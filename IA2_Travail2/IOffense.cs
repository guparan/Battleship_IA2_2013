namespace Battleship
{
using System;
	using System.Drawing;

	public interface IOffense
	{
		void startGame (int[] ship_sizes);

		Point getShot ();

		void shotMiss (Point p);

		void shotHit (Point p);

		void shotSunk (Point p);

		void endGame ();
	}
}
