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
        public const int Max_Volume = 8;
        public const int Cell_Size = 64;
        public const int Cell_Columns = 6;
		public const int Cell_Rows = 6;
		public const Double Water_ClockRate = 700; //evalutate cells every 50ms.
			
    }

    /// <summary>
    /// This is the main type for your game
    /// </summary>
    /// 

    /*Implementation Notes
     *  we assume delta volume (dv) is equivalent to 100 cubic cm. Size of a cell (in pixels) will determine scale.
     *  i.e. if we say 64px = 100cm, then pixel is 6.4mm
     */
    public class Game1 : Microsoft.Xna.Framework.Game
	{
		#region Debug/Test Variables
		int debug_render_scale, debug_x_offset, debug_y_offset;
		#endregion

		#region Graphics Variables
		GraphicsDeviceManager graphics;
        SpriteBatch spriteBatch;
		Texture2D pixel_water, pixel_error, tex2d_grid, tex2d_wall;
		#endregion

		#region UI Variables
		KeyboardState last_kboardstate;
		#endregion

		#region Water CA Variables
		Cell[,] cells;
        int[] cell_vol_to_height; //cell volume to heigh conversion table/array
		
		int field_width;
		int field_height;

		double water_clock;
		#endregion

		public Game1()
        {
            graphics = new GraphicsDeviceManager(this);
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

			#region Debug/Test Variables
			debug_render_scale = 1;
			debug_x_offset = 0;
			debug_y_offset = 0;
			#endregion
			
            last_kboardstate = Keyboard.GetState();

			#region Water CA Initialization
			//set number of columns, how many cells per columns, and the dimensions of the cells. 
			//Field dimensions are derived based on these values. field_dim = cell_count * cell_dim
            water_clock = 0;

			if ( App_Const.Cell_Columns < 3 || App_Const.Cell_Rows < 1 ) //ensure a valid grid is provided
			{
				throw(new ArgumentOutOfRangeException("Cell Dimensions (App_Const.Cell_Columns or App_Const.Cell_Rows)","Value must be greater than 2."));
			}

			field_width = App_Const.Cell_Size * App_Const.Cell_Columns;
			field_height = App_Const.Cell_Size * App_Const.Cell_Rows;

			cells = new Cell[App_Const.Cell_Columns, App_Const.Cell_Rows];
			for ( int row = 0; row < App_Const.Cell_Rows; row++ )
			{
				for ( int col = 0; col < App_Const.Cell_Columns; col++ )
				{
					cells[row, col].x = col * App_Const.Cell_Size;
					cells[row, col].y = row * App_Const.Cell_Size;
					cells[row, col].volume = 0;
					cells[row, col].state = (int)CellState.Empty;					
				}
			}

			cells[0, 0].state = (int)CellState.Wall;
			//cells[1, 0].state = (int)CellState.Null;
			//cells[2, 0].volume = 1;
			//cells[3, 0].volume = 2;
			cells[2, 1].volume = 10;
			//cells[1, 1].volume = 50;
			//cells[2, 1].volume = 75;
			cells[2, 3].volume = 9;
			cells[2, 5].volume = 9;
			//cells[0, 2].volume = 255;
			cells[5, 5].state = (int)CellState.Wall;
			cells[5,0].state=(int)CellState.Wall;


			//precalculate height of volume percents to height in pixels. 
			//Ensures height levels are always same for a given volume accross all cells.
			cell_vol_to_height = new int[App_Const.Max_Volume + 1];
			double cell_div = App_Const.Max_Volume;
			for(int i = 0; i < App_Const.Max_Volume + 1; i++)
			{
				cell_vol_to_height[i] = (int)( App_Const.Cell_Size * ( i / cell_div ) );
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
            KeyboardState kboardstate = Keyboard.GetState();
            // Allows the game to exit
            if (GamePad.GetState(PlayerIndex.One).Buttons.Back == ButtonState.Pressed ||  kboardstate.IsKeyDown(Keys.Escape))
                this.Exit();

			water_clock += gameTime.ElapsedGameTime.TotalMilliseconds;

			if ( water_clock > App_Const.Water_ClockRate )
			{
                water_clock -= App_Const.Water_ClockRate;
				RunCellRules( );
				ResolveCellVolumes( );
			}
			last_kboardstate = kboardstate;
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

            base.Draw(gameTime);
        }

		public void DrawCells (SpriteBatch sbatch)
		{	//actual drawn cell is y-offset by (cell_y + volume). 
			//I do this because otherwise the cell appears 'upside down'
			//for some reason rectangles height grows a rectangle down, not up.
			Cell acell;
			int rec_x, rec_y, rec_height, scale; //position/dimension for rectangle to be drawn.
			//iterate through all cells and draw them.
			spriteBatch.Begin( );

			for ( int row = 0; row < App_Const.Cell_Rows; row++ )
			{
				for ( int col = 0; col < App_Const.Cell_Columns; col++ )
				{
					acell = cells[row, col];
					rec_x = acell.x;
					rec_y = acell.y;
					scale = App_Const.Cell_Size; 
					switch ( acell.state )
					{
						case (int)CellState.Water:
							rec_y = rec_y + scale - cell_vol_to_height[acell.volume];
							rec_height = cell_vol_to_height[acell.volume];
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
							spriteBatch.Draw(	tex2d_grid,
												new Rectangle( rec_x, rec_y, scale, scale ), 
												Color.White );
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

		public void ResolveCellVolumes ( )
		{	//moves 'gained volume' to current volume.
			int total_vol = 0;
			for ( int row = 0; row < App_Const.Cell_Rows; row++ )
			{
				for ( int col = 0; col < App_Const.Cell_Columns; col++ )
				{
					if ( ( cells[row, col].state & ( (int)CellState.Water | (int)CellState.Empty ) ) > 0 )
					{
						cells[row, col].volume += cells[row, col].gained_volume;
						cells[row,col].gained_volume=0;
						total_vol += cells[row, col].volume;
						
					}
				}
			}

			System.Console.WriteLine("Total: " + total_vol.ToString( ) + "\n" );

		}

		public void RunCellRules ()
		{
			//possible future improvement: have water 'schedule' to move, and resolve these volume changes post rule eval.
			int left_neighbor, bottom_neighbor, right_neighbor;
			for ( int row = 0; row < App_Const.Cell_Rows; row++ )
			{
				for ( int col = 0; col < App_Const.Cell_Columns; col++ )
				{	//evaluate neighbors from bottom to left to right
					//world is toroidal in the X direction. (wraps/loops)

					if ( cells[row, col].state == (int)CellState.Water )
					{
						#region Set Neighbors
						if ( col == 0 )
						{ left_neighbor = App_Const.Cell_Columns - 1; }
						else
						{ left_neighbor = col - 1; }

						//set right neighbor
						if ( col == App_Const.Cell_Columns - 1 )
						{ right_neighbor = 0; }
						else
						{ right_neighbor = col + 1; }

						//set bottom neighbor		
						bottom_neighbor = row + 1;
						#endregion

						#region Apply Rules
						
						//rule one, "if neighbor below is empty or has less water than me
						//put as much into it as I can, up to half of my current volume."
						//If it's a wall, do nothing.
						//rule one - [addendum] "If volume is given to bottom neighbor
						//don't give anything to other neighbors this round.
						
						
						int cell_vol = cells[row, col].volume;
						int left_vol = cells[row, left_neighbor].volume;
						int right_vol = cells[row, right_neighbor].volume;
						
						int left_vol_diff= cells[row, col].volume - cells[row, left_neighbor].volume;
						int right_vol_diff = cells[row, col].volume - cells[row, right_neighbor].volume;

						bool left_safe = (cells[row, left_neighbor].state == (int)CellState.Water) 
										|| (cells[row, left_neighbor].state  == (int)CellState.Empty );
						bool right_safe = ( cells[row, right_neighbor].state == (int)CellState.Water )
										|| ( cells[row, right_neighbor].state == (int)CellState.Empty );
						bool both_safe = left_safe && right_safe;

						if (bottom_neighbor < App_Const.Cell_Rows )
						{
							int bot_vol_miss = App_Const.Max_Volume - cells[bottom_neighbor, col].volume; //how much is missing from bot neighbor

							if ( ( ( cells[bottom_neighbor, col].state == (int)CellState.Water )
								|| ( cells[bottom_neighbor, col].state == (int)CellState.Empty ) )
								&& bot_vol_miss > 0 )
							{
								int bot_vol_diff = ( bot_vol_miss >= cells[row, col].volume ) ? cells[row, col].volume : 0; //if we have volume, thats the diff to give.
								cells[row, col].volume -= bot_vol_diff;
								cells[bottom_neighbor, col].volume += bot_vol_diff;
								continue;
							}
						}

						//move up to 2 volume per round.
						//if left and right need volume, and have more than 2 volume, move volumes.
						//if left or right has more volume, and the other has less, move one volume for it.

						if (left_safe&&left_vol_diff>right_vol_diff && left_vol_diff>1&&cell_vol>1)
						{
							System.Console.WriteLine( "Col "+col+" <-MOVE \n" );
							cells[row,left_neighbor].volume+=1;
							cells[row,col].gained_volume-=1;
							continue;
						}

						if (col==0&&row==8)
						{ }

						if (right_safe&&right_vol_diff>=1&&cell_vol>1)
						{ //check things about left and right
							System.Console.WriteLine( "Col "+col+" MOVE-> \n" );
							cells[row,right_neighbor].volume+=1;
							cells[row,col].gained_volume-=1;
							continue;
						}

						if ( both_safe )
						{
							if ( left_vol_diff == right_vol_diff)
							{
								/*if(left_vol_diff == 1 && cell_vol > 0)
								{
									System.Console.WriteLine( "Col " + col + " vDROPv \n");
									cells[row, col].volume -= 1;
									continue;
								}
								

								if ( left_vol_diff == -1 && cell_vol > 0 )
								{
									System.Console.WriteLine( "Col " + col + " ^BUMP^\n" );
									cells[row, col].volume += 1;
									continue;
								}
								*/

								if(left_vol_diff > 0 && cell_vol > 2)
								{
									System.Console.WriteLine( "Col " + col + " <-SPLIT-> \n" );
									cells[row, left_neighbor].volume += 1;
									cells[row, right_neighbor].volume += 1;
									cells[row, col].volume -= 2;
									continue;
								}
							}

							if ( left_vol < cell_vol && right_vol > cell_vol )
							{
								System.Console.WriteLine( "Col " + left_neighbor.ToString( ) + " <<STEAL- " + right_neighbor.ToString( ) + "\n" );
								cells[row, left_neighbor].volume += 1;
								cells[row, right_neighbor].volume -= 1;
								continue;
							}
							
							if ( right_vol <= cell_vol && left_vol > cell_vol )
							{
								System.Console.WriteLine( "Col " + left_neighbor.ToString( ) + " -STEAL>> " + right_neighbor.ToString( ) + "\n" );
								cells[row, left_neighbor].volume -= 1;
								cells[row, right_neighbor].volume+= 1;
								continue;
							}
							
						}

						/*
						if ((right_vol_diff==1||left_vol_diff==1)
								&&(left_safe== false||right_safe== false)
								&&(cell_vol>1))
						{
							System.Console.WriteLine( "Col "+col+" vDROPv \n" );
							cells[row,col].volume-=1;

						}

						if ((right_vol_diff==-1&&left_vol_diff==-1)
								&&left_safe == false &&right_safe == false&&cell_vol>0)
						{
							System.Console.WriteLine( "Col "+col+" ^ADD^ \n" );
							cells[row,col].volume+=1;

						}
						*/
						#endregion

					}					
				}
			}
		}
	}
}
