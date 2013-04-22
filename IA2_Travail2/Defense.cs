// Module de la bataille navale Dreadnought qui prend en charge la défense.
// Placement intelligent des bateaux sur la grille de jeu pour éviter les
// tirs ennemis.

namespace Battleship
{
	using System;
	using System.Collections;
	using System.Collections.Generic;
	using System.Collections.ObjectModel;
	using System.Drawing;

	// TODO: Simplify if opponent never plays ships that touch.
	// TODO: try parity-even only or parity-odd only.

	public class Defense : IDefense
	{
		int w, h;					// Dimensions de la grille de jeu (width et height)
		Random rand = new Random ();

		// Option utilisées pour le choix d'un placement de bateaux parmis plusieurs
		bool place_notouching = false;   // si vrai, les placements avec des bateaux adjacents sont fortemant pénalisés.
		bool standard_touching = false;  // si vrai, le contact entre les bateaux est ignoré. Sinon, faible pénalité en cas de contact.
		// Info : otherwise, we thin out touching to about 1/4 of generated boards.

		/* Statistiques retenues sur le comportement de l'adversaire */
		// Nombre de coups déjà tirés dans la partie
		int nshots_in_game;
		// Tableau utilisé pour stocker la fréquence des tirs ennemis à chaque coordonnée de la grille de jeu.
		int[,] opponent_shots;

		public Defense (Size size, List<String> options)
		{
			w = size.Width;
			h = size.Height;
			place_notouching = options.Exists (x => x == "place_notouching");
			standard_touching = options.Exists (x => x == "standard_touching");
			opponent_shots = new int[w, h];
		}

		// Départ du jeu : choix d'un placement pour les bateaux de tailles données dans la liste en paramètre
		public List<Ship> startGame (int[] ship_sizes)
		{
			List<Ship> placement = placeShips (ship_sizes); // Placement des bateaux de tailles données en paramètre
			print_placement (placement);			// Affichage du placement choisi
			nshots_in_game = 0;					// Initialisation du nombre de coups tirés
			return placement;
		}

		// Enregistre les coordonnées d'un tir adverse pour optimiser le placement à la prochaine partie
		public void shot (Point p)
		{
			// Incrémentation de opponent_shots à la coordonnées du tir
			// S'il y a eu moins de 50 coups dans la partie, on accorde plus d'importance au tir
			// (l'adversaire est susceptible de jouer plus souvent sur ce point)
			opponent_shots [p.X, p.Y] += Math.Max (1, 50 - nshots_in_game);
		  
			// Incrémentation du nombre total de coups tirés dans la partie
			nshots_in_game++;
		}

		public void endGame ()
		{
		}


		// Renvoie une liste de bateaux placés intelligemment dans la grille
		private List<Ship> placeShips (int[] ship_sizes)
		{
			int max_opp_shots = 0;	// Plus grand nombre de coups tirés par l'adversaire sur un même point
			foreach (int x in opponent_shots)
				max_opp_shots = Math.Max (max_opp_shots, x);
			max_opp_shots++;  		// Précaution pour éviter la division par 0
		  
			#if DEBUG
			Console.WriteLine("square shot scores");
			for (int y = 0; y < h; y++)
			{
				for (int x = 0; x < w; x++) {
				  Console.Write("{0,-4} ", 1000 * opponent_shots[x, y] / max_opp_shots);
				}
				Console.WriteLine();
			}
			#endif
		  
			// Génération aléatoire de 100 placements : liste de 100 listes de bateaux
			const int N = 100;
			List<List<Ship>> allocations = new List<List<Ship>> ();

			for (int n = 0; n < N; n++) {
				List<Ship> allocation = new List<Ship> ();	// Nouvelle liste de bateaux
				foreach (int size in ship_sizes) {	// Pour toutes les tailles de bateaux demandées...
					Ship s = new Ship (size);	// Nouveau bateau de taille 'size'
					while (true) {
						// Position et orientation aléatoires
						int x = rand.Next (w);
						int y = rand.Next (h);
						int orient = rand.Next (2);
						s.Place (new Point (x, y), (ShipOrientation)orient);

						// Si le placement obtenu n'est pas valide on repars au début de la boucle while
						if (!s.IsValid (new Size (w, h)))
							continue;

						// S'il n'y a pas de conflits entre les bateaux on sort de la boucle while
						bool ok = true;
						foreach (Ship t in allocation) {
							if (s.ConflictsWith (t)) {
								ok = false;
								break;
							}
						}
						if (ok)
							break;
					}
					allocation.Add (s);			// Ajout du bateau à la liste
				}
				allocations.Add (allocation);	// Ajout de la liste à allocations
			}
		  
			/* Notation des 100 placements et choix du meilleur :
			   Pour chaque cas on incrémente un score selon différents critères.
			   Le meilleur score (celui qu'on garde) est le plus faible.
			*/
			int minscore = 1000000000;				// Score minimum initialisé avec une grande valeur
			List<Ship> min_allocation = null;		// Placement optimal : correspond au score minimum

			foreach (List<Ship> allocation in allocations) {	// Pour chaque placement proposé...
				int score = 0;						// Initialisation du score à 0
				foreach (Ship s in allocation)		// Pour chaque bateau s...
				{
					foreach (Point p in s.GetAllLocations()) {	// Pour chaque point appartenant au bateau...
						// Incrémentation du score en fonction du nombre de tirs adverses sur ce point
						score += 100 * opponent_shots [p.X, p.Y] / max_opp_shots;
					}
					foreach (Ship t in allocation) {	// Pour chaque bateau t...
						// Si s et t sont adjacents et que l'option standard_touching est désactivée : faible pénalité
						if (!standard_touching && shipsAdjacent (s, t))
							score += 20;
						// Si s et t sont adjacents et que l'option place_notouching est activée : forte pénalité
						if (place_notouching && shipsAdjacent (s, t))
							score += 1000000;
					}
				}
				score += rand.Next(15);		// Ajout d'une faible valeur aléatoire pour éviter les doublons
				if (score < minscore)		// Si le score obtenu est meilleur que minscore, on le prend comme référence.
				{
					minscore = score;				// Nouveau score min de référence.
					min_allocation = allocation;	// Nouveau placement optimal
				}
			}
			return min_allocation;
		}


		// Retourne vrai si les bateaux en paramètre sont adjacents
		private bool shipsAdjacent (Ship s, Ship t)
		{
			foreach (Point p in s.GetAllLocations())	// Pour tout point appartenant au bateau s...
			{
				// On regarde si les points 4-voisins sont sur le bateau t
				if (t.IsAt (p + new Size (1, 0)))
					return true;
				if (t.IsAt (p + new Size (-1, 0)))
					return true;
				if (t.IsAt (p + new Size (0, 1)))
					return true;
				if (t.IsAt (p + new Size (0, -1)))
					return true;
			}
			return false;
		}


		// Affichage du placement des bateaux de la liste en paramètre
		private void print_placement (List<Ship> ships)
		{
			int adj = 0;	// Nombre d'adjacence
			for (int i = 0; i < ships.Count; i++) {
				Ship s = ships [i];
				for (int j = i+1; j < ships.Count; j++) {
					Ship t = ships [j];
					if (shipsAdjacent (s, t))
						adj++;
				}
			}

			// Tableau de caractère représentant la grille de jeu
			char[,] placement = new char[w, h];

			// Initialisation : '.' indique un point libre
			for (int x = 0; x < w; x++) {
				for (int y = 0; y < h; y++) {
					placement [x, y] = '.';
				}
			}

			// Représentation des bateaux : les '.' sont remplacés par des nombres indiquant la longueur des bateaux
			foreach (Ship s in ships) {
				foreach (Point p in s.GetAllLocations()) {
					placement [p.X, p.Y] = (char)('0' + s.Length);
				}
			}

		#if DEBUG
			// Mode DEBUG : affichage en console
			Console.WriteLine("placement {0}:", adj);
			for (int y = 0; y < h; y++) {
				Console.Write("  ");
				for (int x = 0; x < w; x++) {
				 	Console.Write(placement[x,y]);
				}
				Console.WriteLine();
			}
		#endif
		}
	}	// fin class Defense
}		// fin namespace Battleship
