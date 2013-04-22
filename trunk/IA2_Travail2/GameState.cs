namespace Battleship
{
	using System;
	using System.Collections;
	using System.Collections.Generic;
	using System.Collections.ObjectModel;
	using System.Drawing;

	public class GameState
	{
		Size boardSize;				// Taille de la grille de jeu
		SeaState[,] state;			// Tableau (dim. 2) des états 'Seastate' de chaque point de la grille de jeu
		List<int> orig_ship_sizes;	// Liste des tailles des bateaux au départ du jeu

		Dictionary<int,int> size_counts;	// La valeur de size_count[i] est le nombre de bateaux de taille i

		// Positions possibles pour les bateaux (l'information de position est contenue dans la classe Ship).
		// positions[i] contient une liste de bateaux de taille i dont les positions sont définies.
		Dictionary<int,List<Ship>> positions;

		// Liste de possibilités d'agencements de bateaux ayant au moins un point touché
		public List<List<Ship>> ship_possibilities;

		// Constructeur
		public GameState(int w, int h, int[] ship_sizes) {
			// Instanciation des attributs
			boardSize = new Size(w, h);
			state = new SeaState[w, h];
			orig_ship_sizes = new List<int> ();
			size_counts = new Dictionary<int,int> ();
			positions = new Dictionary<int,List<Ship>>();
			ship_possibilities = new List<List<Ship>>();
			ship_possibilities.Add(new List<Ship>());

			// Comptage des bateaux par longueur dans size_counts
			foreach (int s in ship_sizes) {		// Pour chaque bateau s de ship_sizes...
				orig_ship_sizes.Add(s);			// Ajout de s dans la liste 'orig_ship_sizes' des tailles initiales

				// Incrémentation de size_counts[s]
				if (!size_counts.ContainsKey(s)) size_counts[s] = 0;
				size_counts[s]++;
			}

			// Définition de la taille du plus grand bateau
			int max_size = 0;
			foreach (int size in orig_ship_sizes)
				if (size > max_size)
					max_size = size;

			// Définition des positions possibles pour chacun des bateaux
			foreach (int len in size_counts.Keys) {		// Pour toutes les longueurs de bateaux...
				positions[len] = new List<Ship>();		// Instanciation d'une liste de positions possibles

				// On parcourt l'ensemble de la grille
				for (int x = 0; x < w; x++) {			// Boucle sur la largeur de la grille...
					for (int y = 0; y < h; y++) {		// Boucle sur la hauteur de la grille...
						for (int orient = 0; orient < 2; orient++) {	// Boucle sur les 2 orientations possibles...
							int dy = orient;	// Si vertical, dy = orient = 1. Sinon dy = 0
							int dx = 1 - dy;	// Si horizontal, dx = 1-dy = 1. Sinon dx = 0

							// Si on sort de la grille, on passe à la position suivante
							if (x + len * dx > w) continue; // Si position horizontale + longueur*dx > largeur grille, itération suivante
							if (y + len * dy > h) continue; // Si position verticale + longueur*dy > largeur grille, itération suivante

							// Cas favorable : ajout du bateau avec ce positionnement à la liste des possibilités
							Ship s = new Ship(len);
							s.Place(new Point(x, y), (ShipOrientation)orient);
							positions[len].Add(s);
						}
					}
				}
			}
		}

		// Indique si l'état du jeu est valide (true) ou non (false)
		public bool valid() {
			// Pas de place pour tous les bateaux
			foreach (int size in size_counts.Keys) {	// Pour toutes les tailles de bateaux...
				if (positions[size].Count < size_counts[size]) return false;  // S'il n'y a pas de place pour tous les bateaux de cette taille : invalide
			}
			// Problème avec les possibilités d'agencements de bateaux restantes
			if (ship_possibilities.Count == 0)
				return false;
			// Cas favorable
			return true;
		}

		// Obtenir l'état 'Seastate' d'un point
		public SeaState get(int x, int y) {
			if (!valid()) throw new ApplicationException("get on bad state");	// Exception : état de jeu non valide
			return state[x, y];
		}
		public SeaState get (Point p)
		{
			return get (p.X, p.Y);
		}

		// Renvoi la liste des tailles des bateaux qui n'ont pas été touchés
		public List<int> remaining_ship_sizes() {
			if (!valid()) throw new ApplicationException("get on bad state");	// Vérification : validité du jeu
			int max_size = 0;							// Taille du plus grand bateau
			foreach (int size in orig_ship_sizes)
				if (size > max_size)
					max_size = size;
			int[] orig_histo = new int[max_size + 1];	// Nombre de bateaux à l'origine pour chaque taille (entre 1 et max_size)
			foreach (int size in orig_ship_sizes)
				orig_histo [size]++;

			int[] max_histo = new int[max_size + 1];
			int[] histo = new int[max_size + 1];

			foreach (List<Ship> list in ship_possibilities) {	// Pour chaque agencement (=liste) de bateaux de ship_possibilities...
				for (int i = 0; i <= max_size; i++) histo[i] = orig_histo[i];	// Copie de orig_histo dans histo
				foreach (Ship s in list) {	// Pour chaque bateau de la liste (bateau touché au moins une fois)...
					histo[s.Length]--;		// Décrémentation de histo pour cette longueur de bateau
				}
				// A cet instant, histo indique le nombre de bateaux qui n'ont pas été touchés pour chaque longueur

				// Définition de max_histo : nombre maxi de bateaux non touchés pour chaque longueur
				for (int i = 0; i <= max_size; i++) {
					if (histo [i] > max_histo [i])
						max_histo [i] = histo [i];
				}
			}

			List<int> sizes = new List<int>();
			for (int i = 0; i <= max_size; i++) {			// Pour toutes les tailles de 0 à max_size...
				for (int j = 0; j < max_histo[i]; j++) {	// De 0 au nombre maxi de bateaux de longueur i non touchés
					sizes.Add(i);			// On ajoute la taille i à la liste sizes : on considère qu'un bateau de taille i est intact
				}
			}
		#if DEBUG
			Console.Write ("remaining sizes:");
			foreach (int s in sizes)
				Console.Write (" {0}", s);
			Console.WriteLine ();
		#endif
			return sizes;
		}

		// Ajout d'un coup manqué
		public void addMiss(Point p) {
			if (state[p.X, p.Y] != SeaState.CLEAR)	// Si l'état en ce point est différent de CLEAR
			{
				ship_possibilities.Clear();			// Nettoyage la liste ship_possibilities
				return;
			}
			state[p.X, p.Y] = SeaState.MISS;		// Changement de l'état SeaState du point à MISS
			updatePossibilitiesMiss(p);				// Mise à jour de ship_possibilities 

			// Suppression de tout placement possible contenant le coup manqué
			foreach (List<Ship> list in positions.Values) {	// Pour tous les placements possibles...
				int j = 0;
				for (int i = 0; i < list.Count; i++) {		// Pour chaque placement proposé pour ce bateau...
					Ship s = list[i];
					// Si le point n'est pas sur le bateau, l'agencement reste valable, il est placé en j
					if (!s.IsAt(p)) list[j++] = s;
				}
				list.RemoveRange(j, list.Count - j);	// Suppression des placements obsolètes (après j)
			}
		}

		// Ajout d'un coup réussi : bateau touché
		public void addHit(Point p) {
			if (state[p.X, p.Y] != SeaState.CLEAR)	// Si l'état en ce point est différent de CLEAR
			{
				ship_possibilities.Clear();			// Nettoyage la liste ship_possibilities
				return;
			}
			state[p.X, p.Y] = SeaState.HIT;			// Changement de l'état SeaState du point à HIT
			updatePossibilitiesHit(p);				// Mise à jour de ship_possibilities
		}

		// Ajout d'un coup réussi : bateau coulé
		public void addSunk(Point p) {
			if (state[p.X, p.Y] != SeaState.CLEAR)	// Si l'état en ce point est différent de CLEAR
			{
				ship_possibilities.Clear();			// Nettoyage la liste ship_possibilities
				return;
			}
			state[p.X, p.Y] = SeaState.SUNK;		// Changement de l'état SeaState du point à SUNK
			updatePossibilitiesSunk(p);				// Mise à jour de ship_possibilities
		}

		// Affichage de l'état du jeu (grille avec les SeaState) et des positions éventuelles de bateaux touchés 
		public void print()
		{
			// Grille de jeu avec les SeaState
			for (int y = 0; y < boardSize.Height; y++) {
				for (int x = 0; x < boardSize.Width; x++) {
					string c = "";
					if (state[x,y] == SeaState.CLEAR) c = "C";
					if (state[x,y] == SeaState.MISS)  c = "M";
					if (state[x,y] == SeaState.HIT)   c = "H";
					if (state[x,y] == SeaState.SUNK)  c = "S";
					Console.Write("--{0}--", c);
				}
				Console.WriteLine ();
			}

			// Positions éventuelles de bateaux touchés
			Console.WriteLine("ship possibilities: {0}", ship_possibilities.Count);
			// S'il y a moins de 10 possibilités, on affiche les détails
			if (ship_possibilities.Count <= 10) {
				foreach (List<Ship> list in ship_possibilities) {	// Affichage des listes possibles
					Console.Write ("   ");
					foreach (Ship s in list) {
						Console.Write (" {0}({1},{2},{3})", s.Length, s.Location.X, s.Location.Y, s.Orientation);
					}
					Console.WriteLine ();
				}
			}
		}

		private static Size[] dirs = {new Size (1, 0), new Size (-1, 0),
		new Size (0, 1), new Size (0, -1)};

		// Renvoie les points voisins du point en paramètre
		private IEnumerable<Point> getNeighbors(Point p) {
			foreach (Size d in dirs) {
				Point q = p + d;
				// On vérifie que le point est bien dans la grille
				if (q.X >= 0 && q.X < boardSize.Width && q.Y >= 0 && q.Y < boardSize.Height) yield return q;
			}
		}

		// Renvoie vrai si les bateaux en paramètre sont adjacents, faux sinon.
		public bool adjacent(Ship s, Ship t) {
			foreach (Point p in s.GetAllLocations()) {
				foreach (Point q in getNeighbors(p)) {
					if (t.IsAt (q))
						return true;
				}
			}
			return false;
		}

		// Renvoie vrai si le bateau en paramètre est coulé
		public bool isSunk(Ship s) {
			if (!valid()) throw new ApplicationException("isSunk on bad state");
			foreach (Point p in s.GetAllLocations()) {				// Pour chaque point du bateau...
				if (state[p.X, p.Y] == SeaState.SUNK) return true;	// Si le SeaState du point est SUNK, alors true
			}
			return false;	// Si aucun SeaState n'est à SUNK, le bateau n'est pas coulé
		}

		// Renvoie vrai si tous les bateaux de la liste sont coulés
		public bool allSunk(List<Ship> list) {
			foreach (Ship s in list) {
				if (!isSunk (s))
					return false;
			}
			return true;
		}

		// Renvoie vrai si le point p est sur un des bateaux de la liste
		private bool isAt(List<Ship> list, Point p) {
			foreach (Ship s in list) {
				if (s.IsAt (p))
					return true;
			}
			return false;
		}

		// Probabilité qu'une configuration de bateau donnée apparaisse
		// dans une configuration de la grille de jeu (états SeaState connus).
		// indexé par (longueur du bateau, nb d'états CLEAR)
		private static double[][] probs = new double[][] {
			new double[]{},
			new double[]{},
			new double[]{1, 1.0/16},   						// bateau de taille 2
			new double[]{1, 1.0/4, 1.0/32}, 				// bateau de taille 3
			new double[]{1, 1.0/4, 1.0/8, 1.0/32},  		// bateau de taille 4
			new double[]{1, 1.0/4, 1.0/8, 1.0/16, 1.0/32},	// bateau de taille 5
		};

		// Retourne la valeur de probs[longueur du bateau s][nb d'états CLEAR des points de s]
		public double probability(Ship s) {
			int clear_cnt = 0;		// Nombre d'états CLEAR pour les points du bateau
			foreach (Point p in s.GetAllLocations()) {				// Pour chaque point du bateau...
				if (state[p.X, p.Y] == SeaState.CLEAR) clear_cnt++;	// Si l'état est CLEAR, on incrémente clear_cnt
			}
			return probs [s.Length] [clear_cnt];
		}

		// Probabilité générée à partir d'une liste de bateaux
		public double probability(List<Ship> list) {
			double r = 1;		// Initialisation de la proba à 1
			foreach (Ship s in list) {		// Pour chaque bateau s de la liste
				r *= probability(s);		// On multiplie r par la proba associée au bateau
			}
			foreach (Ship s in list) {		// Pour chaque bateau s de la liste
				foreach (Ship t in list) {	// Pour chaque bateau t de la liste
					if (s == t) continue;			// Si s == t on passe au t suivant
					if (adjacent(s, t)) r *= 0.5;	// Si s et t sont adjacents on réduit la probabilité de moitié
				}
			}
			return r;
		}

		// Mise à jour de ship_possibilities suite à un coup manqué
		void updatePossibilitiesMiss(Point p) {
			int i, j = 0;
			for (i = 0; i < ship_possibilities.Count; i++)	// Pour chaque liste de bateaux dans ship_possibilities...
			{
				List<Ship> list = ship_possibilities[i];
				// Si le point n'est pas sur un des bateaux, on gardera cette liste, elle est toujours valable
				if (!isAt(list, p)) ship_possibilities[j++] = list;
			}
			// Aprés ce tri, les listes valables sont situés entre les indices 0 et j et les autres listes sont obsolètes
			ship_possibilities.RemoveRange(j, ship_possibilities.Count - j);	// Elimination des listes placées après l'indice j
		}

		// Mise à jour de ship_possibilities suite à un coup réussi : bateau coulé
		void updatePossibilitiesSunk (Point p)
		{
			// We take advantage of the fact that no ships are length 1, so
			// any sinking must be of a ship already hit, and thus already
			// in the possibilities array.
			int j = 0;
			for (int i = 0; i < ship_possibilities.Count; i++)	// Pour chaque liste de bateaux dans ship_possibilities...
			{
				List<Ship> list = ship_possibilities [i];

				// Recherche du bateau coulé
				Ship hit_ship = null;
				foreach (Ship s in list) {
					if (s.IsAt (p)) {
						hit_ship = s;
					}
				}
				if (hit_ship == null)	// Si aucun bateau à cet emplacement pour cette liste : liste invalide...
					continue;			// on passe à la liste suivante (celle ci sera écrasée ou supprimée)

				// Vérification : les autres points du bateau ont bien été touchés
				bool valid = true;
				foreach (Point q in hit_ship.GetAllLocations())	// Pour tous les points du bateau...
				{
					if (q == p)		// Si c'est le nouveau point on passe au suivant
						continue;
					if (state [q.X, q.Y] != SeaState.HIT)	// Si l'état du point est différent de HIT...
					{
						valid = false;	// cette liste est invalide, elle sera éliminée de ship_possibilities
						break;			// on ne parcourt par las autres points du bateau
					}
				}
				if (!valid)	// si la liste n'est pas valide, on passe à la suivante, celle-ci sera écrasée ou supprimée
					continue;

				ship_possibilities [j++] = list;	// cas favorable : on place cette liste en j et on incrémente j
			}
			ship_possibilities.RemoveRange (j, ship_possibilities.Count - j);
		}

		// Mise à jour de ship_possibilities suite à un coup réussi : bateau touché
		void updatePossibilitiesHit (Point p)
		{
			// This is the hard one.  If a hit was on a ship in the list,
			// check a few things.  Otherwise, we need to add to the list
			// all possible ships/positions that can cover the new hit.

			// Nouvelle liste de possibilités qui remplacera ship_possibilities
			List<List<Ship>> new_possibilities = new List<List<Ship>> ();

			foreach (List<Ship> list in ship_possibilities)	// Pour chaque liste de bateaux dans ship_possibilities...
			{
				// Recherche du bateau qui a été touché
				Ship hit_ship = null;
				foreach (Ship s in list) {
					if (s.IsAt (p)) {
						hit_ship = s;
					}
				}

				if (hit_ship != null)	// Si un bateau a été trouvé...
				{
					// Vérification : le bateau n'a pas été entièrement touché (coulé)
					foreach (Point q in hit_ship.GetAllLocations())	// Pour chaque point du bateau...
					{
						if (state [q.X, q.Y] == SeaState.CLEAR) 	// Si le SeaState est CLEAR : OK, bateau non coulé
						{
							new_possibilities.Add (list);	// On ajoute cette liste aux nouvelles possibilités
							break;		// On arrête la recherche, on sait que le bateau n'est pas coulé
						}
					}
					continue;
				}

				// Autres cas : tir en dehors des bateaux de la liste.
				// Ajout de toutes les nouvelles positions possibles
				// d'un bateau qui intersecte ce point
				// (création de plusieurs listes).
				List<int> t = new List<int> (orig_ship_sizes); // Liste des tailles des bateaux au départ du jeu

				foreach (Ship s in list)	// Pour chaque bateau déjà placé dans la liste list...
					t.Remove (s.Length);	// on supprime la valeur correspondante dans t
				// t est donc la liste des tailles des bateaux non placés dans cette liste de bateaux

				// Liste sans doublons des tailles des bateaux non placés dans cette liste de bateaux
				List<int> possible_sizes = new List<int> ();
				foreach (int v in t) {					// pour chaque valeur v de t
					if (!possible_sizes.Contains (v))	// si v n'est pas déjà dans possible_size
						possible_sizes.Add (v);			// ajout de v dans possible_size
				}

				// ...
				foreach (int size in possible_sizes)	// Pour chaque taille possible de bateau non placé
				{
					for (int offset = 0; offset < size; offset++)
					{
						for (int orient = 0; orient < 2; orient++)
						{
							int dy = orient;
							int dx = 1 - dy;
							int x = p.X - offset * dx;
							int y = p.Y - offset * dy;
							if (x < 0 || y < 0)
								continue;
							if (x + size * dx > boardSize.Width)
								continue;
							if (y + size * dy > boardSize.Height)
								continue;
							bool valid = true;
							for (int i = 0; i < size; i++) {
								if (i == offset)
									continue;  // the new hit
								if (state [x + i * dx, y + i * dy] != SeaState.CLEAR) {
									valid = false;
									break;
								}
							}
							if (!valid)
								continue;
							Ship s = new Ship (size);
							s.Place (new Point (x, y), (ShipOrientation)orient);
							foreach (Ship w in list) {
								if (s.ConflictsWith (w)) {
									valid = false;
									break;
								}
							}
							if (!valid)
								continue;
							List<Ship> new_list = new List<Ship> (list);
							new_list.Add (s);
							new_possibilities.Add (new_list);
						}
					}
				}
			}

			// Enregistrement des nouvelle possibilités dans ship_possibilities
			ship_possibilities = new_possibilities;
		}

	}	// fin class GameState
}		// fin namespace Battleship
