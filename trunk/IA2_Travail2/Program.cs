namespace Battleship
{
    using System;
	using System.Drawing;
	using System.Linq;

	class Program
	{
		static void Main (string[] args)
		{
			// Instanciation des deux joueurs
			var op1 = new Dreadnought ();
			var op2 = new RandomOpponent ();

			// Instanciation de la compétition
			BattleshipCompetition bc = new BattleshipCompetition (
                op1,
                op2,
                new TimeSpan (0, 0, 1),  // Temps limite par jeu
                1,                       // Nombre de parties gagnantes nécessaires
                true,                    // Jouer tous les matchs ?
                new Size (10, 10),       // Taille de la grille
                2, 3, 3, 4, 5            // Liste des tailles des bâteaux
			);

			// Lancement de la compétition
			var scores = bc.RunCompetition ();

			// Affichage des scores
			foreach (var key in scores.Keys.OrderByDescending(k => scores[k])) {
				Console.WriteLine ("{0} {1}:\t{2}", key.Name, key.Version, scores [key]);
			}

			Console.ReadKey(true);
		}
	}
}
