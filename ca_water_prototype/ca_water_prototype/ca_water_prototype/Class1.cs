using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ca_water_prototype
{
	//Cell represents a space within the world which maintains a state.
	//Cells can be affected by rules and made to take on new states based on these rules.
	//Rules are evaluted on cells at regular discrete time intervals (i.e. every 50ms)
	
	public enum CellState
	{
		Null=0, Wall = 1, Empty = 2, Water = 4, 
		//insert additional states here.
		Max //do not add anything after max.
	};

	/// <summary>
	/// Cells in this prototype have three states: Wall, Water, Empty.
	/// 
	/// Volume: sybte value representing the percent amount of the square which is full. Valid for 0 - 100.
	/// When it's a Wall or it's Empty, Volume is always 0.
	/// 
	/// </summary>
	public struct Cell
	{
		
		public int x, y, gained_volume;
		public int _state, _volume;
		public int state
		{
			get	{ return _state;}
			set 
			{	//state must be a valid enumeration of State.
				if ( value > (int)CellState.Max )
				{ this._state = (int)CellState.Max - 1; }
				else if ( value < (int)CellState.Null ) 
				{ this._state = (int)CellState.Null; }
				else 
				{ this._state = value; }

				if ( _state != (int)CellState.Water)
				{ this._volume = 0; } //if set to empty, volume is zero.
			}

		}

		public int volume
		{
			get 
			{
				return _volume;
			}
			set
			{	//ensure the volume value is always [0,100]
				if ( value > App_Const.Max_Volume )
				{ _volume = App_Const.Max_Volume; }
				else if ( value < 0 )
				{ _volume = 0; }
				else
				{ _volume = value; }

				if ( _volume == 0 )
				{ _state = (int)CellState.Empty; } //If volume is zero, cell is state Empty.
				else
				{ _state = (int)CellState.Water; }
			}

		}

		public Cell ( int px, int py, int pstate = 0, int pvolume = 0, int pgainvol = 0 )
		{
			x = px;
			y = py;
			_state = pstate;
			_volume = pvolume;
			gained_volume = pgainvol;
		}

	}
}
