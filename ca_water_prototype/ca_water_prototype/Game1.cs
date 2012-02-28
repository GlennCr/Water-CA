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
		//amount which a cells mass can increase under compression per number of blocks above it.
        public const int Max_Mass = Cell.Max_Mass;  		//anything over this value is subject to compression checks
        public const int Min_Mass = Cell.Min_Mass;			//anything under this val is culled	
        
        public const int MinDelta = (int)(Max_Mass * .5f);
		public const Double Compress_Rate = 0.00;	//rate we compress by
		public const int MaxCompress = (int)(Max_Mass * Compress_Rate); 
		public const int Cell_Size = 32;		//in pixels
        public const int Cell_OffsetX = 0;		//X offset of the grid.
		public const int Cell_OffsetY = 0;		//Y offset of the grid.
		public const int Cell_Columns = 30;		//determines the height of the map aswell.
		public const int Cell_Rows = 20;		//determines the width of the map aswell.
		
		public const Double mass_to_height = (Cell_Size / (Double)Max_Mass); //cell mass to heigh conversion table/array

		public const Double Water_ClockRate = 10; //evalutate cells every XXXXms.
		
		#endregion

		#region Graphics Variables
		GraphicsDeviceManager graphics;
        SpriteBatch spriteBatch;
		Texture2D pixel_water, pixel_error, tex2d_grid, tex2d_wall, tex2d_bg;
		SpriteFont sf_segoe;
		#endregion

		#region UI Variables
		KeyboardState last_kboardstate;
		MouseState last_mousestate;
		Vector2 cursor_pos;
		#endregion

		#region Water CA Variables
		Cell[,] cells;
		
		int field_width;
		int field_height;
		
		double water_clock;
		int has_drawn;
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

        protected override void Initialize()
        {
            base.Initialize();

            last_kboardstate = Keyboard.GetState( );
			last_mousestate = Mouse.GetState( );

			#region Water CA Initialization
			//set number of columns, how many cells per columns, and the dimensions of the cells. 
			//Field dimensions are derived based on these values. field_dim = cell_count * cell_dim
            water_clock = 0;
			has_drawn = 0;

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
			cells[2, 1].mass = Max_Mass;
			//cells[1, 1].mass = 50;
			//cells[2, 1].mass = 75;
			cells[2, 2].mass = Max_Mass;
			//cells[0, 2].mass = 255;
			cells[Cell_Rows - 1, Cell_Columns - 1].state = (int)CellState.Wall;
			cells[Cell_Rows - 1, 0].state=(int)CellState.Wall;
			#endregion


		}

		protected override void LoadContent ( )
        {
            // Create a new SpriteBatch, which can be used to draw textures.
            spriteBatch = new SpriteBatch(GraphicsDevice);
			GraphicsDevice.BlendState = BlendState.Opaque;
            pixel_water = Content.Load<Texture2D>( "water" );
			pixel_error = Content.Load<Texture2D>( "error" );
			tex2d_wall = Content.Load<Texture2D>( "wall" );
			tex2d_grid = Content.Load<Texture2D>( "grid" );
			tex2d_bg = Content.Load<Texture2D>( "noisebg" );
			sf_segoe = Content.Load<SpriteFont>( "segoe" );
        }

        protected override void UnloadContent()
        {
            // TODO: Unload any non ContentManager content here
        }


        protected override void Update(GameTime gameTime)
        {
            KeyboardState kboardstate = Keyboard.GetState( );
			MouseState mousestate = Mouse.GetState( );
            // Allows the game to exit
            if (GamePad.GetState(PlayerIndex.One).Buttons.Back == ButtonState.Pressed ||  kboardstate.IsKeyDown(Keys.Escape))
                this.Exit();

			//holding shift and clicking will remove anything in the highlighted cell.
			if (mousestate.RightButton == ButtonState.Pressed)
			{	//when Right clicking, add blocks of water
				Vector2 gridpos = GridPosition( mousestate.X, mousestate.Y );
				if (OnGrid( gridpos.X, gridpos.Y ))
				{
					int cellindy = GetCellInd(gridpos.Y, Cell_Rows, Cell_OffsetY);
					int cellindx = GetCellInd(gridpos.X, Cell_Columns, Cell_OffsetX);
					if (kboardstate.IsKeyDown( Keys.LeftShift ))
					{
						cells[cellindy, cellindx].mass = 0;
						cells[cellindy, cellindx].state = (int)CellState.Empty;
					}
					else
					{
						cells[cellindy, cellindx].state = (int)CellState.Water;
						cells[cellindy, cellindx].mass = Max_Mass;
					}
				}
			}

			if (mousestate.LeftButton == ButtonState.Pressed)
			{	//when left clicking, add walls.
				Vector2 gridpos = GridPosition( mousestate.X, mousestate.Y );
				if (OnGrid( gridpos.X, gridpos.Y ))
				{
					int cellindy = GetCellInd(gridpos.Y, Cell_Rows, Cell_OffsetY);
					int cellindx = GetCellInd(gridpos.X, Cell_Columns, Cell_OffsetX);
					if (kboardstate.IsKeyDown( Keys.LeftShift ))
					{
						cells[cellindy, cellindx].mass = 0;
						cells[cellindy, cellindx].state = (int)CellState.Empty;
					}
					else
					{
						cells[cellindy, cellindx].mass = 0;
						cells[cellindy, cellindx].state = (int)CellState.Wall;
					}
				}
			}

			
			water_clock += gameTime.ElapsedGameTime.Milliseconds;
			//run as many times as needed to keep pace.
			if (water_clock > Water_ClockRate)
			{
				water_clock -= Water_ClockRate;
				RunCellRules( );
				CountMass( );
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

			//draw the background
			spriteBatch.Begin( );
			spriteBatch.Draw( tex2d_bg, new Rectangle( 0, 0, field_width, field_height ), Color.AliceBlue );
			spriteBatch.End( );

			DrawCells( spriteBatch );


			//Draw cursor and debug text at the cursor.
			spriteBatch.Begin( );
			Vector2 snapPos = GridPosition( last_mousestate.X, last_mousestate.Y ); //snap cursor to grid
			bool isongrid = OnGrid( snapPos.X, snapPos.Y );
			
			if (isongrid)
			{
				int cellindrow= GetCellInd(snapPos.Y, Cell_Rows, Cell_OffsetY);
				int cellindcol= GetCellInd(snapPos.X, Cell_Columns, Cell_OffsetX );
				String output = String.Format("C [{0}:{1}-> {2}"
							,(int)( ( (snapPos.Y - Cell_OffsetY) / Cell_Size) % Cell_Rows),
							(int)( ( (snapPos.X - Cell_OffsetX) / Cell_Size) % Cell_Columns),
							cells[ cellindrow, cellindcol].StateToString());
				spriteBatch.DrawString(sf_segoe, output, new Vector2(snapPos.X + 3, snapPos.Y - 15), Color.Black);
				spriteBatch.Draw( tex2d_grid, snapPos, new Rectangle( 0, 0, Cell_Size, Cell_Size ), Color.White );
			}

			spriteBatch.End( );

			has_drawn++;
            base.Draw(gameTime);
        }

		public void DrawCells (SpriteBatch sbatch)
		{	//actual drawn cell is y-offset by (cell_y + [a ratio of mass to height] ). 
			//I do this because otherwise the cell appears 'upside down'
			//for some reason a rectangles height grows the rectangle down, not up.
			Cell acell;
			int rec_x, rec_y, scale; //position/dimension for rectangle to be drawn.
			
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
							int cell_height = 0;
							if( acell.mass > Max_Mass || ( row > 0 && cells[row - 1, col].mass > Min_Mass * 10))
							{ //keep everything drawing within the bounds of a cell.
								cell_height = Cell_Size;
							}
							else
								cell_height = (int)(acell.mass * mass_to_height);

							int depth_color = 0;//(int)MathHelper.Clamp(acell.mass * .1f, 0.005f, 50f); //change color based on depth. (NOT USED CURRENTLY)
							int mass_alpha = (int)MathHelper.Clamp(acell.mass*.03f + MathHelper.Clamp((acell.mass - Max_Mass)*.3f, 0f, 100f), 15, 100f); //change alpha based on depth.

							rec_y = rec_y + scale - cell_height;
							
							spriteBatch.Draw( pixel_water, new Rectangle( rec_x, rec_y, scale, cell_height ), 
												new Color(20, 80, 145, 90 + mass_alpha) );
							//uncommenting the below line causes the mass of a cell to be rendered as a string. use only if cells are 32x32 or larger!
							//spriteBatch.DrawString(sf_segoe, acell.StateToString( ) + "\n" + depth_color, new Vector2(acell.x + (Cell_Size / 5), acell.y + (Cell_Size / 4)), Color.Gray);
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

		public void CountMass ( )
		{	//moves 'gained mass' to current mass.
			int total_vol = 0;
			for ( int row = 0; row < Cell_Rows; row++ )
			{	for ( int col = 0; col < Cell_Columns; col++ )
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
			//possible future improvement: have water 'schedule' to move, and resolve these mass changes post rule eval. -- done
			for ( int row = 0; row < Cell_Rows; row++ )
			{	for ( int col = 0; col < Cell_Columns; col++ )
				{	//evaluate neighbors from bottom to left to right
					//water leaving the map is deleted
					if (cells[row, col].is_fillable)
					{
						int deltaMass = 0;
						int remainingMass = cells[row, col].mass;
						int deltaUpperBound = 0; //when determining the deltaMass to move around, we may want to bound it to a max delta.

						if (remainingMass < Min_Mass) { continue; } //if there isn't enough mass to work with, we'll ignore this block. Done after every rule.

						//consider block below (if it exists)
						if (row < Cell_Rows-1 && cells[row+1, col].is_fillable)
						{	//For mass we can move to bottom. We subtract the mass of the bottom cell to avoid over fill.
							deltaMass = CompressibleMass( remainingMass + cells[row+1, col].mass ) - cells[row+1, col].mass; 
							if (deltaMass > MinDelta) { deltaMass = (deltaMass / 2); } //if it's moving too fast, div by 2

							//make sure a cell never moves more mass than it has, or move mass down faster than it can.
							// (0 <= deltaMass <= deltaUpperBound)
							deltaUpperBound = Math.Min(MinDelta, remainingMass); 
							deltaMass = (deltaMass < 0) ? 0 : (deltaMass < deltaUpperBound) ? deltaMass : deltaUpperBound;

							cells[row, col].future_mass -= deltaMass;
							cells[row+1, col].future_mass += deltaMass;
							remainingMass -= deltaMass;

							if (cells[row+1,col].state == (int)CellState.Empty || cells[row+1,col].mass < cells[row,col].mass)
							{ continue; }

						} else if (row == Cell_Rows-1)
						{	//if cell is against the edge of map, it loses mass to 'the void'
							deltaMass = CompressibleMass( remainingMass);
							//if (deltaMass > MinDelta) { deltaMass = (deltaMass / 2); }
							deltaMass = (deltaMass < 0) ? 0 : (deltaMass < remainingMass) ? deltaMass : remainingMass;
							cells[row, col].future_mass -= deltaMass;
							remainingMass -= deltaMass;
						}

						if (remainingMass < Min_Mass) { continue; }

						//consider the left neighbor (if it exists)
						if (col > 0 && cells[row, col-1].is_fillable)
						{	
							deltaMass = (int)(cells[row, col].mass - cells[row, col-1].mass) / 4;

							if (deltaMass > MinDelta) { deltaMass = (deltaMass / 2); }
							//make sure a cell never moves more mass than it has.
							// (0 <= deltaMass <= remainingMass)
							deltaMass = (deltaMass < 0) ? 0 : (deltaMass < remainingMass) ? deltaMass : remainingMass;

							cells[row, col].future_mass -= deltaMass;
							cells[row, col-1].future_mass += deltaMass;
							remainingMass -= deltaMass;

						} else if (col == 0)
						{ //if cell is against the edge of map, it loses mass to 'the void'
							deltaMass = cells[row, col].mass / 2;
							if (deltaMass > MinDelta) { deltaMass = (deltaMass / 2); }
							deltaMass = (deltaMass < 0) ? 0 : (deltaMass < remainingMass) ? deltaMass : remainingMass;
							cells[row, col].future_mass -= deltaMass;
							remainingMass -= deltaMass;
						}

						if (remainingMass < Min_Mass) { continue; }

						//consider the right neighbor (if it exists)
						if (col < Cell_Columns-1 && cells[row, col+1].is_fillable)
						{
							deltaMass = (int)(cells[row, col].mass - cells[row, col+1].mass) / 4 ;
							
							if (deltaMass > MinDelta) {deltaMass = (deltaMass / 2); } //if it's moving to fast, slow it down by half.
							//make sure a cell never moves more mass than it has.
							//(0 <= deltaMass <= remainingMass)
							deltaMass = (deltaMass < 0) ? 0 : (deltaMass < remainingMass) ? deltaMass : remainingMass;

							cells[row, col].future_mass -= deltaMass;
							cells[row, col+1].future_mass += deltaMass;
							remainingMass -= deltaMass;

						} else if (col == Cell_Columns-1)
						{ //if cell is against the edge of map, it loses mass to 'the void'
							deltaMass = cells[row, col].mass / 2;
							if (deltaMass > MinDelta) { deltaMass = (deltaMass / 2); }
							deltaMass = (deltaMass < 0) ? 0 : (deltaMass < remainingMass) ? deltaMass : remainingMass;
							cells[row, col].future_mass -= deltaMass;
							remainingMass -= deltaMass;
						}

						if (remainingMass < Min_Mass) { continue; }

						//consider cell above us (if it exists) Cell only adds to above cell if it has compressed mass)
						if (row > 0 && cells[row-1, col].is_fillable)
						{
							deltaMass = remainingMass - CompressibleMass( remainingMass + cells[row-1, col].mass );
							
							//if (deltaMass > MinDelta) { deltaMass = (deltaMass / 2); } //if it's moving to fast, slow it down by half.
							
							// (0 <= deltaMass <= deltaUpperBound)
							deltaUpperBound = Math.Min( MinDelta, remainingMass );
							deltaMass = (deltaMass < 0) ? 0 : (deltaMass < deltaUpperBound) ? deltaMass : deltaUpperBound;

							cells[row, col].future_mass -= deltaMass;
							cells[row-1, col].future_mass += deltaMass;
							remainingMass -= deltaMass;

						}
						
					}
				}
			}

			//set masses to future mass
			for (int row = 0; row < Cell_Rows; row++)
			{	for (int col = 0; col < Cell_Columns; col++)
				{
					if(cells[row,col].is_fillable)
						cells[row, col].UpdateMass( );
				}
			}

		}

		public int CompressibleMass ( int totalMass) //simulating Compressible portion of Navier-Stokes equations.
		{
			if (totalMass <= Max_Mass) { return Max_Mass; }

			if (totalMass < (Max_Mass * 2) + MaxCompress)
			{ 
				return (Max_Mass * Max_Mass + totalMass*MaxCompress)/(Max_Mass + MaxCompress); 
				//return (Max_Mass / MaxCompress) + (totalMass / Max_Mass); 
			}

			return (totalMass + MaxCompress) / 2 ;
		}

		public Vector2 GridPosition ( int x, int y )
		{	//spits out a position on the grid, with grid offset. 
			int xout = (int)Math.Floor( x / (double)Cell_Size ) * Cell_Size + Cell_OffsetX;
			int yout = (int)Math.Floor( y / (double)Cell_Size ) * Cell_Size + Cell_OffsetY;

			return new Vector2( xout, yout );
		}

		public Vector2 GridPosition ( float x, float y )
		{	//spits out a position on the grid, with grid offset. 
			int xout = (int)Math.Floor( x / (double)Cell_Size ) * Cell_Size + Cell_OffsetX;
			int yout = (int)Math.Floor( y / (double)Cell_Size ) * Cell_Size + Cell_OffsetY;

			return new Vector2( xout, yout );
		}

		/// <summary>
		/// Returns the Cell located on the (row/col). Giving Y and Row values gives you location on Row. X and Column gives location on Column
		/// </summary>
		/// <param name="pos"></param>
		/// <param name="divs"></param>
		/// <returns></returns>
		public int GetCellInd ( int pos, int divs, int offset=0 )
		{			
			return (int)((pos - offset) / Cell_Size) % divs;
		}

		/// <summary>
		/// Returns the Cell located on the (row/col). Giving Y and Row values gives you location on Row. X and Column gives location on Column
		/// </summary>
		/// <param name="pos"></param>
		/// <param name="divs"></param>
		/// <returns></returns>
		public int GetCellInd ( float pos, int divs, int offset=0 )
		{			
			return (int)(((int)pos - offset) / Cell_Size) % divs;
		}

		/// <summary>
		/// Returns true if the location provided is on the 'grid'
		/// </summary>
		/// <param name="x"></param>
		/// <param name="y"></param>
		/// <returns></returns>
		public bool OnGrid ( int x, int y )
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

		/// <summary>
		/// Returns true if the location provided is on the 'grid'
		/// </summary>
		/// <param name="x"></param>
		/// <param name="y"></param>
		/// <returns></returns>
		public bool OnGrid ( float x, float y )
		{	//checks if the position given is on the grid.
			if (x < Cell_OffsetX || (int)x >= (Cell_Columns * Cell_Size + Cell_OffsetX))
			{
				return false;
			}

			if (y < Cell_OffsetY || (int)y >= (Cell_Rows * Cell_Size + Cell_OffsetY))
			{
				return false;
			}

			return true;
		}

	}
}
