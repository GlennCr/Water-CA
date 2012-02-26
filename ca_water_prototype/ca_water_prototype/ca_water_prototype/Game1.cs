using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Audio;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.GamerServices;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Microsoft.Xna.Framework.Media;

namespace ca_water_prototype
{

    public static class App_Const
    {
		public const int Max_Mass = 1000;		//anything over this value is subject to compression checks
		public const int Min_Mass = 2;			//anything under this val is culled	
    }

    /// <summary>
    /// This is the main type for your game
    /// </summary>
    /// 

    /*Implementation Notes
     *  we assume delta mass (dv) is equivalent to 1000 cubic mm. Size of a cell (in pixels) will determine scale.
     *  i.e. if we say 64px = 100cm, then pixel is 6.4mm
     */
    public class Game1 : Microsoft.Xna.Framework.Game
	{
		#region Constants
		//amount which a cell can increase by under compression per number of blocks above it.		
		public const Double Compress_Rate = 0.02;	//rate 
		public const int Cell_Size = 64;		//in pixels
        public const int Cell_OffsetX = 0;		//X offset of the grid.
		public const int Cell_OffsetY = 32;		//Y offset of the grid.
		public const int Cell_Columns = 6;		//determines the height of the map aswell.
		public const int Cell_Rows = 6;			//determines the width of the map aswell.
		
		public const Double Water_ClockRate = 700; //evalutate cells every XXXXms.
		
		#endregion

		#region Graphics Variables
		GraphicsDeviceManager graphics;
        SpriteBatch spriteBatch;
		Texture2D pixel_water, pixel_error, tex2d_grid, tex2d_wall;
		SpriteFont sf_segoe;
		#endregion

		#region UI Variables
		KeyboardState last_kboardstate;
		MouseState last_mousestate;
		Vector2 cursor_pos;
		#endregion

		#region Water CA Variables
		Cell[,] cells;
        int[] cell_vol_to_height; //cell mass to heigh conversion table/array
		
		int field_width;
		int field_height;
		
		double water_clock;
		#endregion

		public Game1()
        {
            graphics = new GraphicsDeviceManager(this);
			field_width = Cell_Size * Cell_Columns + Cell_OffsetX;
			field_height = Cell_Size * Cell_Rows + Cell_OffsetY;
			graphics.PreferredBackBufferWidth = field_width;
			graphics.PreferredBackBufferHeight = field_height;
            Content.RootDirectory = "Content";
        }

        /// <summary>
        /// Allows the game to perform any initialization it needs to before starting to run.
        /// This is where it can query for any required services and load any non-graphic
        /// related content.  Calling base.Initialize will enumerate through any components
        /// and initialize them as well.
        /// </summary>
        protected override void Initialize()
        {
            base.Initialize();

            last_kboardstate = Keyboard.GetState();

			#region Water CA Initialization
			//set number of columns, how many cells per columns, and the dimensions of the cells. 
			//Field dimensions are derived based on these values. field_dim = cell_count * cell_dim
            water_clock = 0;

			if ( Cell_Columns < 3 || Cell_Rows < 3 ) //ensure a valid grid is provided
			{
				throw(new ArgumentOutOfRangeException("Cell Dimensions (Cell_Size or Cell_Rows)","Value must be greater than 2."));
			}

			cells = new Cell[Cell_Rows, Cell_Columns];
			for ( int row = 0; row < Cell_Rows; row++ )
			{
				for ( int col = 0; col < Cell_Columns; col++ )
				{
					cells[row, col].x = col * Cell_Size + Cell_OffsetX;
					cells[row, col].y = row * Cell_Size + Cell_OffsetY;
					cells[row, col].mass = 0;
					cells[row, col].state = (int)CellState.Empty;					
				}
			}

			//cells[1, 0].state = (int)CellState.Null;
			//cells[2, 0].mass = 1;
			//cells[3, 0].mass = 2;
			cells[2, 1].mass = App_Const.Max_Mass;
			//cells[1, 1].mass = 50;
			//cells[2, 1].mass = 75;
			cells[2, 2].mass = App_Const.Max_Mass;
			//cells[0, 2].mass = 255;
			cells[Cell_Rows - 1, Cell_Columns - 1].state = (int)CellState.Wall;
			cells[Cell_Rows - 1, 0].state=(int)CellState.Wall;


			//precalculate height of mass percents to height in pixels. 
			//Ensures height levels are always same for a given mass accross all cells.
			cell_vol_to_height = new int[App_Const.Max_Mass + 1];
			for(int i = 0; i < App_Const.Max_Mass + 1; i++)
			{
				cell_vol_to_height[i] = (int)( Cell_Size * ( i / App_Const.Max_Mass) );
			}
			#endregion


		}

		/// <summary>
		/// LoadContent will be called once per game and is the place to load
		/// all of your content.
		/// </summary>
		protected override void LoadContent ( )
        {
            // Create a new SpriteBatch, which can be used to draw textures.
            spriteBatch = new SpriteBatch(GraphicsDevice);

            pixel_water = Content.Load<Texture2D>( "water" );
			pixel_error = Content.Load<Texture2D>( "error" );
			tex2d_wall = Content.Load<Texture2D>( "wall" );
			tex2d_grid = Content.Load<Texture2D>( "grid" );
			sf_segoe = Content.Load<SpriteFont>( "segoe" );
        }

        /// <summary>
        /// UnloadContent will be called once per game and is the place to unload
        /// all content.
        /// </summary>
        protected override void UnloadContent()
        {
            // TODO: Unload any non ContentManager content here
        }

        /// <summary>
        /// Allows the game to run logic such as updating the world,
        /// checking for collisions, gathering input, and playing audio.
        /// </summary>
        /// <param name="gameTime">Provides a snapshot of timing values.</param>
        protected override void Update(GameTime gameTime)
        {
            KeyboardState kboardstate = Keyboard.GetState( );
			MouseState mousestate = Mouse.GetState( );
            // Allows the game to exit
            if (GamePad.GetState(PlayerIndex.One).Buttons.Back == ButtonState.Pressed ||  kboardstate.IsKeyDown(Keys.Escape))
                this.Exit();

			water_clock += gameTime.ElapsedGameTime.TotalMilliseconds;
			if ( water_clock > Water_ClockRate )
			{
                water_clock -= Water_ClockRate;
				RunCellRules( );
				ResolveCellMasss( );
			}
			
			last_kboardstate = kboardstate;
			last_mousestate = mousestate;

			base.Update(gameTime);
        }

        /// <summary>
        /// This is called when the game should draw itself.
        /// </summary>
        /// <param name="gameTime">Provides a snapshot of timing values.</param>
        protected override void Draw(GameTime gameTime)
        {
            GraphicsDevice.Clear(Color.Black);

			DrawCells( spriteBatch );

			spriteBatch.Begin( );
			Vector2 snapPos = GridPosition( last_mousestate.X, last_mousestate.Y ); //snap cursor to grid
			bool isongrid = OnGrid( snapPos.X, snapPos.Y );
			String output = String.Format("( {0},{1})\n" 
							+"C [{2}:{3}]\n",snapPos.X, snapPos.Y,Math.Floor(snapPos.Y / Cell_Size),Math.Floor(snapPos.X / Cell_Size));

			spriteBatch.DrawString(sf_segoe, output, new Vector2(snapPos.X + 3, snapPos.Y - 15), Color.White);
			if (isongrid)
			{ spriteBatch.Draw( tex2d_grid, snapPos, new Rectangle( 0, 0, Cell_Size, Cell_Size ), Color.Wheat ); }

			spriteBatch.End( );

            base.Draw(gameTime);
        }

		public void DrawCells (SpriteBatch sbatch)
		{	//actual drawn cell is y-offset by (cell_y + mass). 
			//I do this because otherwise the cell appears 'upside down'
			//for some reason rectangles height grows a rectangle down, not up.
			Cell acell;
			int rec_x, rec_y, rec_height, scale; //position/dimension for rectangle to be drawn.
			//iterate through all cells and draw them.
			spriteBatch.Begin( );

			for ( int row = 0; row < Cell_Rows; row++ )
			{
				for ( int col = 0; col < Cell_Columns; col++ )
				{
					acell = cells[row, col];
					rec_x = acell.x;
					rec_y = acell.y;
					scale = Cell_Size; 
					switch ( acell.state )
					{
						case (int)CellState.Water:
							rec_y = rec_y + scale - cell_vol_to_height[acell.mass];
							rec_height = cell_vol_to_height[acell.mass];
							spriteBatch.Draw(	pixel_water,
												new Rectangle( rec_x, rec_y, scale, rec_height ), 
												Color.White );
							break;
						case (int)CellState.Wall:
							spriteBatch.Draw(	tex2d_wall,
												new Rectangle( rec_x, rec_y, scale, scale ), 
												Color.White );
							break;
						case (int)CellState.Empty:
							//spriteBatch.Draw(	tex2d_grid,
							//					new Rectangle( rec_x, rec_y, scale, scale ), 
							//					Color.White );
							break;
						case (int) CellState.Null:
							spriteBatch.Draw(	pixel_error,
												new Rectangle( rec_x, rec_y, scale, scale ), 
												Color.White );
							break;
						default:
							spriteBatch.Draw(	pixel_error,
												new Rectangle( rec_x, rec_y, scale, scale ), 
												Color.White );
							break;
					}
				}
			}

			spriteBatch.End( );
		}

		public Cell GetCell (int x, int y)
		{
			/*x / field.width;
			 *y / field.height;
			 *lookup cell in vector list and return it.
			 */
			return new Cell(); //some cell
		}

		public void ResolveCellMasss ( )
		{	//moves 'gained mass' to current mass.
			int total_vol = 0;
			for ( int row = 0; row < Cell_Rows; row++ )
			{
				for ( int col = 0; col < Cell_Columns; col++ )
				{
					if ( ( cells[row, col].state & ( (int)CellState.Water | (int)CellState.Empty ) ) > 0 )
					{
						total_vol += cells[row, col].mass;
						
					}
				}
			}

			System.Console.WriteLine("Total: " + total_vol.ToString( ) + "\n" );

		}

		public void RunCellRules ()
		{
			//possible future improvement: have water 'schedule' to move, and resolve these mass changes post rule eval.
			int neighbor_mass;
			for ( int row = 0; row < Cell_Rows; row++ )
			{
				for ( int col = 0; col < Cell_Columns; col++ )
				{	//evaluate neighbors from bottom to left to right
					//water leaving the map is deleted
					if (cells[row, col].is_fillable)
					{
					}
				}
			}

			//set masses to future mass

		}

		public Vector2 GridPosition ( int x, int y )
		{	//spits out a position on the grid, with grid offset. 
			int xout = (int)Math.Floor( x / (double)Cell_Size ) * Cell_Size + Cell_OffsetX;
			int yout = (int)Math.Floor( y / (double)Cell_Size ) * Cell_Size + Cell_OffsetY;

			return new Vector2( xout, yout );
		}

		public bool OnGrid ( int x, int y )
		{	//checks if the position given is on the grid.
			if (x < Cell_OffsetX || x > (Cell_Columns * Cell_Size + Cell_OffsetX))
			{
				return false;
			}

			if (y < Cell_OffsetY || y > (Cell_Rows * Cell_Size + Cell_OffsetY))
			{
				return false;
			}

			return true;
		}

		public bool OnGrid ( float x, float y )
		{	//checks if the position given is on the grid.
			if (x < Cell_OffsetX || x >= (Cell_Columns * Cell_Size + Cell_OffsetX))
			{
				return false;
			}

			if (y < Cell_OffsetY || y >= (Cell_Rows * Cell_Size + Cell_OffsetY))
			{
				return false;
			}

			return true;
		}

	}
}
