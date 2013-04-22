namespace Battleship
{
	using System;
	using System.Collections.Generic;
	using System.Diagnostics;
	using System.Drawing;
	using System.Linq;

	public class BattleshipCompetition
	{
		private IBattleshipOpponent op1;	// Joueur 1
		private IBattleshipOpponent op2;	// Joueur 2
		private TimeSpan timePerGame;		// Durée maximale du jeu
		private int wins;					// Nombre de parties gagnantes nécessaires
		private bool playOut;				// Jouer tous les matchs ?
		private Size boardSize;				// Taille de la grille
		private List<int> shipSizes;		// Liste des tailles des bateaux

		// Constructeur
		public BattleshipCompetition (IBattleshipOpponent op1, IBattleshipOpponent op2, TimeSpan timePerGame, int wins, bool playOut, Size boardSize, params int[] shipSizes)
		{
			// Gestion des cas d'erreur
			if (op1 == null) {
				throw new ArgumentNullException ("op1");
			}

			if (op2 == null) {
				throw new ArgumentNullException ("op2");
			}

			if (timePerGame.TotalMilliseconds <= 0) {
				throw new ArgumentOutOfRangeException ("timePerGame");
			}

			if (wins <= 0) {
				throw new ArgumentOutOfRangeException ("wins");
			}

			if (boardSize.Width <= 2 || boardSize.Height <= 2) {
				throw new ArgumentOutOfRangeException ("boardSize");
			}

			if (shipSizes == null || shipSizes.Length < 1) {
				throw new ArgumentNullException ("shipSizes");
			}

			if (shipSizes.Where (s => s <= 0).Any ()) {
				throw new ArgumentOutOfRangeException ("shipSizes");
			}

			if (shipSizes.Sum () >= (boardSize.Width * boardSize.Height)) {
				throw new ArgumentOutOfRangeException ("shipSizes");
			}

			// Cas général : affectation des attributs
			this.op1 = op1;
			this.op2 = op2;
			this.timePerGame = timePerGame;
			this.wins = wins;
			this.playOut = playOut;
			this.boardSize = boardSize;
			this.shipSizes = new List<int> (shipSizes);
		}


		// Déroulement de la compétition
		public Dictionary<IBattleshipOpponent, int> RunCompetition ()
		{
			var rand = new Random ();

			var opponents = new Dictionary<int, IBattleshipOpponent> ();	// Joueurs
			var scores = new Dictionary<int, int> ();						// Scores des joueurs : Nb de parties gagnées
			var times = new Dictionary<int, Stopwatch> ();					// Temps pris par chaque joueur sur une partie
			var ships = new Dictionary<int, List<Ship>> ();					// bateaux des joueurs
			var shots = new Dictionary<int, List<Point>> ();				// Mémoire des coups tirés par les joueurs

			var first = 0;		// Le joueur 1 est celui d'indice 0 dans opponents
			var second = 1;		// Le joueur 2 est celui d'indice 1 dans opponents

			// Initialisation des variables
			opponents [first] = this.op1;
			opponents [second] = this.op2;
			scores [first] = 0;
			scores [second] = 0;
			times [first] = new Stopwatch ();
			times [second] = new Stopwatch ();
			shots [first] = new List<Point> ();
			shots [second] = new List<Point> ();

			// Choix aléatoire du joueur qui commence : 1 chance sur 2 d'inverser l'ordre
			if (rand.NextDouble () >= 0.5)	// rand.NextDouble() génère un nombre aléatoire entre 0 et 1
			{
				var swap = first;
				first = second;
				second = swap;
			}

			// Nouveau match pour chacun des joueurs
			opponents [first].NewMatch (opponents [second].Name + " " + opponents [second].Version.ToString ());
			opponents [second].NewMatch (opponents [first].Name + " " + opponents [first].Version.ToString ());

			bool success;

			// Déroulement de la compétition
			while (true) {
				/* Condition d'arrêt de la compétition
				 * Si playout = false : on s'arrête lorsqu'un joueur a gagné "wins" parties
				 * Si playout = true : on joue toutes les parties soit (wins * 2 - 1) parties.
				 */
				if ((!this.playOut && scores.Where (p => p.Value >= this.wins).Any ()) || (this.playOut && scores.Sum (s => s.Value) >= (this.wins * 2 - 1))) {
					break;
				}

				// À chaque nouvelle partie on change le joueur qui commence
				{
					var swap = first;
					first = second;
					second = swap;
				}

				// Réinitialisation des temps et des tirs enregistrés
				times [first].Reset ();
				times [second].Reset ();
				shots [first].Clear ();
				shots [second].Clear ();

				// Nouvelle partie pour chacun des joueurs
				times [first].Start ();		// Départ compteur de temps
				opponents [first].NewGame (this.boardSize, this.timePerGame);
				times [first].Stop ();		// Arrêt compteur de temps
				if (times [first].Elapsed > this.timePerGame) {	// Si temps dépassé, le joueur a perdu la partie
					RecordWin (second, first, scores, opponents);
					continue;
				}

				times [second].Start ();
				opponents [second].NewGame (this.boardSize, this.timePerGame);
				times [second].Stop ();
				if (times [second].Elapsed > this.timePerGame) {
					RecordWin (first, second, scores, opponents);
					continue;
				}

				// Placement des bateaux pour le 1er joueur
				success = false;
				do {
					// Création de la liste des bateaux du premier joueur à partir des tailles "shipSizes"
					ships [first] = (from s in this.shipSizes
                                    select new Ship (s)).ToList ();

					times [first].Start ();
					// Placement des bateaux avec la méthode propre au joueur
					opponents [first].PlaceShips (ships [first].AsReadOnly ());
					times [first].Stop ();
					if (times [first].Elapsed > this.timePerGame) {
						break;
					}

					// Vérifications du placement (déjà vérifié dans défense pour Dreadnought, nécessaire pour Random)
					bool allPlacedValidly = true;
					for (int i = 0; i < ships[first].Count; i++) {
						if (!ships [first] [i].IsPlaced || !ships [first] [i].IsValid (this.boardSize)) {
							allPlacedValidly = false;
							break;
						}
					}
                    
					if (!allPlacedValidly) {	// Si invalide : on recommence
						continue;
					}

					bool noneConflict = true;
					for (int i = 0; i < ships[first].Count; i++) {
						for (int j = i + 1; j < ships[first].Count; j++) {
							if (ships [first] [i].ConflictsWith (ships [first] [j])) {
								noneConflict = false;
								break;
							}
						}

						if (!noneConflict) {
							break;
						}
					}

					if (!noneConflict) {	// Si comflit : on recommence
						continue;
					}
					else {
						success = true;		// Placement correct, on peut sortir de la boucle
					}
				} while (!success);

				if (times [first].Elapsed > this.timePerGame) {
					RecordWin (second, first, scores, opponents);
					continue;
				}

				// Placement des bateaux pour le 2ème joueur : même méthode
				success = false;
				do {
					ships [second] = (from s in this.shipSizes
                                     select new Ship (s)).ToList ();

					times [second].Start ();
					opponents [second].PlaceShips (ships [second].AsReadOnly ());
					times [second].Stop ();
					if (times [second].Elapsed > this.timePerGame) {
						break;
					}

					bool allPlacedValidly = true;
					for (int i = 0; i < ships[second].Count; i++) {
						if (!ships [second] [i].IsPlaced || !ships [second] [i].IsValid (this.boardSize)) {
							allPlacedValidly = false;
							break;
						}
					}

					if (!allPlacedValidly) {
						continue;
					}

					bool noneConflict = true;
					for (int i = 0; i < ships[second].Count; i++) {
						for (int j = i + 1; j < ships[second].Count; j++) {
							if (ships [second] [i].ConflictsWith (ships [second] [j])) {
								noneConflict = false;
								break;
							}
						}

						if (!noneConflict) {
							break;
						}
					}

					if (!noneConflict) {
						continue;
					} else {
						success = true;
					}
				} while (!success);

				if (times [second].Elapsed > this.timePerGame) {
					RecordWin (first, second, scores, opponents);
					continue;
				}

				// Déroulement de la partie
				var current = first;
				while (true) {
					times [current].Start ();
					Point shot = opponents [current].GetShot ();	// Récupération d'un point de tir par la méthode du joueur
					times [current].Stop ();
					if (times [current].Elapsed > this.timePerGame) {
						RecordWin (1 - current, current, scores, opponents);
						break;
					}

					// Si le joueur a déjà tiré sur ce point, on recommence (utile pour joueur Random)
					if (shots [current].Where (s => s.X == shot.X && s.Y == shot.Y).Any ()) {
						continue;
					}

					// Mémorisation du point de tir
					shots [current].Add (shot);

					times [1 - current].Start ();
					opponents [1 - current].OpponentShot (shot);	// On effectue le tir sur l'adversaire [1-current]
					times [1 - current].Stop ();
					if (times [1 - current].Elapsed > this.timePerGame) {
						RecordWin (current, 1 - current, scores, opponents);
						break;
					}

					// Variable indiquant s'il y a un bateau au point de tir sur la grille adverse
					var ship = (from s in ships [1 - current]
                                where s.IsAt (shot)
                                select s).SingleOrDefault ();

					// S'il y a un bateau...
					if (ship != null) {
						var sunk = ship.IsSunk (shots [current]); // Indique si le bateau est coulé

						times [current].Start ();
						opponents [current].ShotHit (shot, sunk);	// Notifie le joueur qu'il a touché un bateau en ce point
						times [current].Stop ();
						if (times [current].Elapsed > this.timePerGame) {
							RecordWin (1 - current, current, scores, opponents);
							break;
						}
					}

					// Si pas de bateau
					else {
						times [current].Start ();
						opponents [current].ShotMiss (shot);	// Notifie le joueur que son tir est manqué
						times [current].Stop ();
						if (times [current].Elapsed > this.timePerGame) {
							RecordWin (1 - current, current, scores, opponents);
							break;
						}
					}

					// Indique s'il reste des bateaux non coulés
					var unsunk = (from s in ships [1 - current]
                                  where !s.IsSunk (shots [current])
                                  select s);

					// Si tous les bateaux sont coulés, le joueur courant a gagné, fin de la partie
					if (!unsunk.Any ()) {
						RecordWin (current, 1 - current, scores, opponents);
						break;
					}

					current = 1 - current;	// On change de joueur courant pour le tour suivant

				} // fin de la boucle de la partie
			}	// fin de la boucle de la compétition

			// On notifie les joueur de la fin du match
			opponents [first].MatchOver ();
			opponents [second].MatchOver ();

			// On retourne les scores des joueurs
			return scores.Keys.ToDictionary (s => opponents [s], s => scores [s]);
		}

		// Enregistrement du gagnant
		private void RecordWin (int winner, int loser, Dictionary<int, int> scores, Dictionary<int, IBattleshipOpponent> opponents)
		{
			scores [winner]++;				// Incrémentation du score du gagnant
			opponents [winner].GameWon ();	// Fin de partie gagnante pour le joueur gagnant.
			opponents [loser].GameLost ();	// Fin de partie perdante pour le joueur perdant.
		}

	}	// fin class BattleshipCompetition
}		// fin namespace Battleship
