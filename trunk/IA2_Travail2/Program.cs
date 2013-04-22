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
			var op1 = new DreadnoughtBasic ();
			var op2 = new DreadnoughtImproved ();

			// Instanciation de la compétition
			BattleshipCompetition bc = new BattleshipCompetition (
                op1,					 // Joueur 1
                op2,					 // Joueur 2
                new TimeSpan (0, 0, 1),  // Durée maximale d'une partie
                100,                       // Nombre de parties gagnantes nécessaires
                true,                    // Jouer tous les matchs ?
                new Size (10, 10),       // Taille de la grille
                2, 3, 3, 4, 5            // Liste des tailles des bateaux
			);

			// Lancement de la compétition
			var scores = bc.RunCompetition ();

			// Affichage des scores
			foreach (var key in scores.Keys.OrderByDescending(k => scores[k])) {
				Console.WriteLine ("{0} {1}:\t{2}", key.Name, key.Version, scores [key]);
			}

			Console.ReadKey (true);
		}
	}
}
