// Part of the Dreadnought battleship program which handles
// offense - the choice of shots to sink the opponent's ships.

namespace Battleship
{
	using System;
	using System.Collections;
	using System.Collections.Generic;
	using System.Collections.ObjectModel;
	using System.Drawing;

	public class OffenseBasic : IOffense
	{
		/*********** ATTRIBUTES ***********/

		private int w;
		private int h;
		private Random rand = new Random ();
		public GameState state; // Etat de la grille, conserve les tirs effectues : CLEAR (pas tiré), MISS (rien), HIT(touché), SUNK(coulé)

		private int apriori_types = 2;
		private int apriori_type; // ???
		private int total_ships_size; // Nombre de points occupés par les bateaux

		// option flags
		public bool fully_resolve_hits; // ???
		public bool assume_notouching; // Les bateaux ne se touchent pas

		// statistics kept about opponent's layout behavior
		int shots_in_game; // Nombre de tirs effectués (sur une seule partie)
		int[,] statistics_shot_hit; // Nombre de tirs "touché" en ce point (sur toutes les parties)
		int[,] statistics_shot_miss; // Nombre de tirs "manqué" en ce point (sur toutes les parties)

		private static Size[] dirs = {new Size (1, 0), new Size (-1, 0), new Size (0, 1), new Size (0, -1)};


		/*********** METHODS ***********/

		public OffenseBasic (Size size, List<String> options)
		{
			w = size.Width;
			h = size.Height;
			statistics_shot_hit = new int[w, h];
			statistics_shot_miss = new int[w, h];
#if DEBUG
			print_apriori ();
#endif
			apriori_type = rand.Next (apriori_types);
			apriori_type = 0; // TODO: remove
			fully_resolve_hits = options.Exists (x => x == "fully_resolve_hits");
			assume_notouching = options.Exists (x => x == "assume_notouching");
		}

		// Debut du jeu : init des attributs
		public void startGame (int[] ships_sizes)
		{
			state = new GameState (w, h, ships_sizes);
			total_ships_size = 0;
			foreach (int i in ships_sizes)
				total_ships_size += i;
			shots_in_game = 0;
		}

		// Un tir a retourné "manqué"
		public void shotMiss (Point shot)
		{
			state.addMiss (shot); // On marque cette position comme manquée
			statistics_shot_miss [shot.X, shot.Y]++; // Utilisé pour calculer la fréquence des tirs touchés en un point
		}

		// Un tir a retourné "touché"
		public void shotHit (Point shot)
		{
			state.addHit (shot); // On marque cette position comme touchée
			statistics_shot_hit [shot.X, shot.Y]++;  // Utilisé pour calculer la fréquence des tirs touchés en un point
		}

		// Un tir a retourné "coulé"
		public void shotSunk (Point shot)
		{
			state.addSunk (shot); // On marque cette position comme coulée
			// Si les bateaux ne se touchent pas, on marque tous les points 
			// autour d'un touché ou d'un coulé comme manqués (on économise des coups)
			if (assume_notouching) { 
				foreach (Point p in getAllPoints()) {
					if (state.get (p) == SeaState.HIT || state.get (p) == SeaState.SUNK) {
						foreach (Point q in neighbors(p)) {
							if (state.get (q) == SeaState.CLEAR) { // on ne regarde que les points clear (non tirés)
								state.addMiss (q);
								break;
							}
						}
					}
				}
			}
			statistics_shot_hit [shot.X, shot.Y]++;
		}

		// A la fin du jeu : log des frequences
		public void endGame ()
		{
#if DEBUG
			// L'history probability en un point est la fréquence de tirs touchés (sur toutes les parties)
			// Plus cette valeur est élevée, plus souvent la position est occupée par un bateau adverse
			Console.WriteLine ("history probability");
			for (int y = 0; y < h; y++) {
				Console.Write ("   ");
				for (int x = 0; x < w; x++) {
					double p = history_probability (x, y);
					Console.Write (" {0,-4}", (int)(1000 * p));
				}
				Console.WriteLine ();
			}
#endif
		}

		// Choix du prochain coup
		public Point getShot ()
		{
#if DEBUG
			Console.WriteLine ("getting shot {0}", shots_in_game++);
			state.print ();
#endif
			// Recuperation de la liste des coups interessants
			List<Point> choices = getShot_ExtendShips (); 

			if (choices.Count == 0) { // Si aucun coup interessant n'a été trouvé
				Point r = getShot_Random (); // Choix d'un coup au hasard
#if DEBUG
				foreach (Point q in neighbors(r)) { // On regarde si ce coup est à coté d'un point déjà touché (ou coulé)
					if (state.get (q) == SeaState.HIT || state.get (q) == SeaState.SUNK)
						Console.WriteLine ("adjacent to ship!");
				}
#endif
				return r;
			}
			// cas "normal" : il y a des coups interessants
#if DEBUG
			Console.Write ("extendships ");
			foreach (Point p in choices)
				Console.Write ("{0} ", p); // on affiche ces coups
			Console.WriteLine ();
#endif
			// On choisit le coup joué au hasard parmis les coups interessants
//			return choices [rand.Next (choices.Count)];
			return choices [0];
		}

		// returns all squares on the board.
		private IEnumerable<Point> getAllPoints ()
		{
			for (int x = 0; x < w; x++) {
				for (int y = 0; y < h; y++) {
					yield return new Point (x, y);
				}
			}
		}

		// returns the <= 4 neighbor points of the given point
		//  0
		// 1*2
		//  3
		private IEnumerable<Point> neighbors (Point p)
		{
			foreach (Size d in dirs) {
				Point q = p + d;
				if (q.X >= 0 && q.X < w && q.Y >= 0 && q.Y < h)
					yield return q;
			}
		}

		// public for testing
		// Fonction qui détermine quels coups sont intéressants
		public List<Point> getShot_ExtendShips ()
		{
			List<Point> choices = new List<Point> ();

			// ???
			if (!fully_resolve_hits) {
				foreach (List<Ship> list in state.ship_possibilities) {
					if (state.allSunk (list))
						return choices; // retourne liste vide : on va choisir le coup au hasard
				}
			}

			// algorithm: choose spot which, if a miss, maximizes the
			// number of ship layout possibilities (weighted by probability)
			// which we eliminate.
			double[,] weight = new double[w, h]; // Grille de poids : plus un coup est interessant, plus sont poids sera élevé
			foreach (List<Ship> list in state.ship_possibilities) {
				double wt = state.probability (list); // On récupère la probabilité que ???
				foreach (Ship s in list) {
					foreach (Point p in s.GetAllLocations()) {
						if (state.get (p) == SeaState.CLEAR) {
							weight [p.X, p.Y] += wt;
						}
					}
				}
			}
#if DEBUG
			Console.WriteLine ("weights:");
			for (int y = 0; y < h; y++) {
				for (int x = 0; x < w; x++) {
					// Ecriture des poids à la 6e décimale près
					Console.Write (String.Format (" {0:0.00000}", weight [x, y]));
				}
				Console.WriteLine ();
			}
#endif

			// return maximum weight squares
			double maxw = -1.0;
			Point maxp = new Point ();

			foreach (Point p in getAllPoints()) {
				if (weight [p.X, p.Y] > maxw) {
					maxw = weight [p.X, p.Y];
					maxp.X = p.X;
					maxp.Y = p.Y;
				}
			}

			choices.Add (maxp);
			return choices;
		}

		// Fonction qui choisit un coup au hasard
		private Point getShot_Random ()
		{
//			Console.WriteLine ("Hasard");
			// find out which hits are definitely sunk and which might still be
			// on live ships.
			bool[,] possible_unsunk_hits = new bool[w, h];
			foreach (List<Ship> list in state.ship_possibilities) {
				foreach (Ship s in list) {
					if (state.isSunk (s))
						continue;
					foreach (Point p in s.GetAllLocations()) {
						if (state.get (p) == SeaState.HIT)
							possible_unsunk_hits [p.X, p.Y] = true;
					}
				}
			}
#if DEBUG
			Console.WriteLine ("possible unsunk hits");
			for (int y = 0; y < h; y++) {
				for (int x = 0; x < w; x++) {
					Console.Write ("{0}", possible_unsunk_hits [x, y] ? "..H.." : "..O..");
				}
				Console.WriteLine ();
			}
#endif

			// find out which squares could hold the remaining ships, and if so
			// what the probability of each square is.
			double[,] ship_prob = new double[w, h]; // probabilité qu'il y ait un bateau en chaque point
			foreach (int len in state.remaining_ship_sizes()) {
				double[,] aposteriori_prob = new double[w, h]; // ???
				for (int x = 0; x < w; x++) {
					for (int y = 0; y < h; y++) {
						for (int orient = 0; orient < 2; orient++) {
							int dy = orient;
							int dx = 1 - dy;
							if (x + len * dx > w)
								continue;
							if (y + len * dy > h)
								continue;
							bool good = true;
							for (int i = 0; i < len; i++) {
								SeaState st = state.get (x + i * dx, y + i * dy);
								if (!(st == SeaState.CLEAR ||
									(!fully_resolve_hits && st == SeaState.HIT && possible_unsunk_hits [x + i * dx, y + i * dy]))) {
									good = false;
									break;
								}
							}
							if (!good)
								continue; // On passe à l'autre orientation

							double p = apriori_prob (len, new Point (x, y), (ShipOrientation)orient);
							bool next_to_other_ship = false;
							for (int i = 0; i < len; i++) {
								foreach (Point n in neighbors(new Point(x + i*dx, y + i*dy))) {
									if (state.get (n) == SeaState.HIT || state.get (n) == SeaState.SUNK)
										next_to_other_ship = true;
								}
							}
							if (next_to_other_ship)
								p *= .2;  // TODO: set to .2?
							for (int i = 0; i < len; i++) {
								if (state.get (x + i * dx, y + i * dy) == SeaState.HIT)
									continue;
								double wt = history_probability (x + i * dx, y + i * dy);
								aposteriori_prob [x + i * dx, y + i * dy] += p * wt;
							}
						}
					}
				}
				// normalize aposteriori probability
				if (!normalize_prob (aposteriori_prob)) {
					// this condition triggers when there is no place
					// to put a ship of a particular size.
					continue;
				}

				// TODO: weight sum of probabilities, for example
				// to prioritize the finding of the 2-ship?
				for (int x = 0; x < w; x++) {
					for (int y = 0; y < h; y++) {
						ship_prob [x, y] += aposteriori_prob [x, y];
					}
				}
			}

			// On retient la proba maximale
			double max_prob = 0;
			foreach (double p in ship_prob)
				if (p > max_prob)
					max_prob = p;

#if DEBUG
			Console.WriteLine ("random choice probabilities");
			for (int y = 0; y < h; y++) {
				for (int x = 0; x < w; x++) {
					Console.Write ("{0,-3}{1} ", (int)(ship_prob [x, y] * 1000), ship_prob [x, y] == max_prob ? "*" : " ");
				}
				Console.WriteLine ();
			}
#endif

//			 On récupère tous les points à proba maximale
			if (max_prob == 0)
				return new Point (rand.Next (w), rand.Next (h));

//			List<Point> max_points = new List<Point> ();
			for (int x = 0; x < w; x++) {
				for (int y = 0; y < h; y++) {
					if (ship_prob [x, y] == max_prob) {
						return new Point (x, y);
//						max_points.Add (new Point (x, y));
					}
				}
			}
			// pick random one of the prob maximizing spots
//			return max_points [rand.Next (max_points.Count)];

			throw new Exception ("Aucun max trouvé");
		}

		// ???
		double apriori_prob (int len, Point p, ShipOrientation orient)
		{
			switch (apriori_type) {
			case 0:
					// uniform distribution
				if (orient == ShipOrientation.Horizontal)
					return 1.0 / (w - len + 1) / h;
				else
					return 1.0 / (h - len + 1) / w;
			case 1:
					// weighted towards edge
				double scale = Math.Sqrt (2.0) - 1; // factor of 2.0 from center to edge
				if (orient == ShipOrientation.Horizontal) {
					int min = 0;
					int max = w - len;
					double mid = (max - min) / 2.0;
					double r = 1.0 + scale * Math.Abs (p.X - mid) / (max - mid);

					min = 0;
					max = h - 1;
					mid = (max - min) / 2.0;
					r *= 1.0 + scale * Math.Abs (p.Y - mid) / (max - mid);
					return r;
				} else {
					int min = 0;
					int max = w - 1;
					double mid = (max - min) / 2.0;
					double r = 1.0 + scale * Math.Abs (p.X - mid) / (max - mid);

					min = 0;
					max = h - len;
					mid = (max - min) / 2.0;
					r *= 1.0 + scale * Math.Abs (p.Y - mid) / (max - mid);
					return r;
				}
			default:
				return 0.0;
			}
		}

		// normalizes entries in prob so they total 1.0.
		private bool normalize_prob (double[,] prob)
		{
			double total_prob = 0.0;
			foreach (double x in prob)
				total_prob += x;
			if (total_prob == 0)
				return false;

			for (int x = 0; x < prob.GetLength(0); x++) {
				for (int y = 0; y < prob.GetLength(1); y++) {
					prob [x, y] /= total_prob;
				}
			}
			return true;
		}

		// returns the fraction of shots at this square that resulted
		// in a hit (smoothed somewhat).
		private double history_probability (int x, int y)
		{
			// These estimates are hard to do in general, as we don't pick
			// our sampling points randomly.  But we don't need exact answers
			// here, so we assume our samples were chosen randomly.  We
			// use a simple frequency-based approach.

			int hits = statistics_shot_hit [x, y];
			int misses = statistics_shot_miss [x, y];
			int shots = hits + misses;

			// # of samples to weight prior (17/100) and current data 50/50.
			double fake_shots = 25;
			double total_possible_hits = total_ships_size / (w * h);
			double fake_hits = fake_shots * total_possible_hits;

			double res = (hits + fake_hits) / (shots + fake_shots);
			return res;
		}

		// Fonction qui affiche la probabilité que ??? pour tous les points
		void print_apriori ()
		{
			for (apriori_type = 0; apriori_type < apriori_types; apriori_type++) {
				Console.WriteLine ("apriori type: {0}", apriori_type);
				for (int size = 2; size <= 5; size++) {
					for (int orient = 0; orient < 2; orient++) {
						Console.WriteLine ("  {0}{1}:", size, orient == 0 ? "H" : "V");
						double sum = 0.0;
						for (int y = 0; y < h; y++) {
							Console.Write ("   ");
							for (int x = 0; x < w; x++) {
								double p;
								// si (a partir de ce point un bateau horizontal sort de la grille) 
								// ou (a partir de ce point un bateau vertical sort de la grille)
								if ((orient == 0 && x + size > w) || (orient == 1 && y + size > h)) {
									p = 0.0;
								} else {
									// On calcule la probabilité que ???
									p = apriori_prob (size, new Point (x, y), (ShipOrientation)orient);
								}
								// Affichage des probabilités à la 6e décimale près
								Console.Write (String.Format (" {0:0.######}", p));
								sum += p;
							}
							Console.WriteLine ();
						}
						Console.WriteLine ("    sum: {0}", sum);
					}
				}
			}
		}
	}
}
