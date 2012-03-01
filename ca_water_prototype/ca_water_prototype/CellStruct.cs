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
	{	//enumerated so that we can also use boolean logic if desired.
		Null=0, Wall = 1, Empty = 2, Water = 4, 
		//insert additional states above this comment only..
		Max //do not add anything after max. (nothing should be higher than max)
	};

	/// <summary>
	/// Cells in this prototype have three states: Wall, Water, Empty.
	/// 
	/// Mass: sybte value representing the percent amount of the square which is full. Valid for 0 - 100.
	/// When it's a Wall or it's Empty, Mass is always 0.
	/// 
	/// </summary>
	public struct Cell
	{
		//In the future, set values to something smaller to reduce footprint of arrays of cells. e.g. mass might fit better in a byte. Or use same INT for future_mass and current_mass, but use shifting and bool ops to divvy the variable up.
        public const int Max_Mass = 1000;
        public const int Min_Mass = 3;
        
        public bool is_fillable;
		public int x, y, future_mass;
		private int _state, _mass;
		public int state
		{
			get	{ return _state;}
			set 
			{	//state must be a valid enumeration of State.
				if ( value > (int)CellState.Max )
				{ this._state = (int)CellState.Max / 2; }
				else if ( value < (int)CellState.Null ) 
				{ this._state = (int)CellState.Null; }
				else 
				{ this._state = value; }

				if ( _state != (int)CellState.Water)
				{ this._mass = 0; this.future_mass = 0; } //if set to empty or wall, mass is zero.

				if ( (_state & ( (int)CellState.Wall | (int)CellState.Null)) > 0)
				{ is_fillable = false; }
				else
				{ is_fillable = true; }
			}

		}

		public int mass
		{
			get 
			{
				return _mass;
			}
			set
			{	//ensure the mass value is always valid and culled if needed. If mass is < 2, it's set to 0.
				if ( value < Min_Mass )
				{ _mass = 0; }
				else
				{ _mass = value; }

				if ( _mass < Min_Mass )
				{ _state = (int)CellState.Empty; } //If mass is zero, cell is state Empty.
				else if(is_fillable)
				{ _state = (int)CellState.Water; }

				future_mass = _mass;
			}

		}

		public void UpdateMass()
		{
			mass = future_mass;
		}

		public Cell ( int px = 0, int py = 0, int pstate = 0, int pmass = 0, bool fillable = true, int pgainvol = 0 )
		{
			x = px;
			y = py;
			_state = pstate;
			_mass = pmass;
			future_mass = pgainvol;
			is_fillable = fillable;
		}

		public int alpha_mass
		{
			get
			{
				return (50 * _mass/ Max_Mass);
			}
			private set { }
		}

		public String StateToString ( )
		{
			switch (_state)
			{
				case (int)CellState.Empty: return "Empty";
				case (int)CellState.Wall: return "Wall";
				case (int)CellState.Water: return String.Format("{0}", 10 * _mass/ Max_Mass );

				default: return "NULL";	
			}
		}

	}
}
