namespace Battleship {
	using System;
	using System.Collections;
	using System.Collections.Generic;
	using System.Collections.ObjectModel;
	using System.Drawing;

	public class Dreadnought : IBattleshipOpponent {
    
		public string Name
		{
			get {
				String s = "Dreadnought";
				foreach (String opt in options) {
				  s += "," + opt;
				}
				return s;
			}
		}

		public Version Version { get { return new Version(1, 2); } }

		Size gameSize;			// Dimensions de la grille de jeu
		IOffense offense;		// Gestion de l'attaque
		IDefense defense;		// Gestion de la défense : placement intelligent des bateaux
		public List<String> options = new List<String>();	// Liste d'options : pas utilisé

		public void setOption(String option)
		{
			options.Add(option);
		}

		public void NewMatch(string opponent) { }

		public void NewGame(Size size, TimeSpan timeSpan)
		{
			// Si le nouveau jeu a des dimensions différentes on redéfinit offense, defense et gameSize
			if (size != gameSize)
			{
				offense = new Offense(size, options);
				defense = new Defense(size, options);
				gameSize = size;
			}
		}

		public void PlaceShips(ReadOnlyCollection<Ship> ships)
		{
			// Tableau contenant les tailles de bateaux à placer
			int[] ship_sizes = new int[ships.Count];
			for (int i = 0; i < ships.Count; i++) ship_sizes[i] = ships[i].Length;
			
			// Envoi de ce paramètre à l'attaque et à la défense
			offense.startGame(ship_sizes);
			List<Ship> placement = defense.startGame(ship_sizes); // Génère un placement intelligent

			// Enregistrement du placement généré par la défense
			foreach (Ship s in placement)	// Pour tous les bateaux s du placement généré...
			{
				foreach (Ship t in ships)	// Pour tous les bateaux t de la collection 'ships'...
				{
					if (!t.IsPlaced && t.Length == s.Length)	// Si t n'est pas placé et que sa longueur égale celle de s 
					{
						t.Place(s.Location, s.Orientation);		// On affecte le placement au bateau t
						break;									// On passe au placement suivant
					}
				}
			}
		}

		// Retourne un point de tir choisi intelligemment par le module d'attaque.
		public Point GetShot()
		{
			Point p = offense.getShot();
		#if DEBUG
			Console.WriteLine("shoot at {0},{1}", p.X, p.Y);
		#endif
			return p;
		}

		// Enregistre un tir ennemi. Pris en charge par le module de défense.
		public void OpponentShot(Point shot)
		{
		#if DEBUG
		  	Console.WriteLine("opponent shot {0},{1}", shot.X, shot.Y);
		#endif
		  	defense.shot(shot);
		}

		// Enregistre un tir réussi (touché ou coulé) : module d'attaque.
		public void ShotHit(Point shot, bool sunk)
		{
		#if DEBUG
		  	Console.WriteLine("shot at {0},{1} hit{2}", shot.X, shot.Y, sunk ?  " and sunk" : "");
		#endif
		  	if (sunk) offense.shotSunk(shot);	// Si le tir a coulé un bateau, utilisation de shotSunk
		  	else offense.shotHit(shot);			// Sinon utilisation de shotHit
		}

		// Enregistre un tir manqué : module d'attaque.
		public void ShotMiss(Point shot)
		{
		#if DEBUG
		  	Console.WriteLine("shot at {0},{1} missed", shot.X, shot.Y);
		#endif
		  	offense.shotMiss(shot);
		}

		// Indique que le joueur a gagné la partie
		public void GameWon()
		{
		#if DEBUG
		  	Console.WriteLine("game won");
		#endif
			MatchOver();
		}

		// Indique que le joueur a perdu la partie
		public void GameLost()
		{
		#if DEBUG
		  	Console.WriteLine("game lost");
		#endif
			MatchOver();
		}

		// Termine la partie
		public void MatchOver()
		{
			offense.endGame();
			defense.endGame();
		}
  
	}	// fin class Dreadnought
}		// fin namespace Battleship
