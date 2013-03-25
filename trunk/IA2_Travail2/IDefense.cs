namespace Battleship {
  using System;
  using System.Collections.Generic;
  using System.Drawing;
  
  public interface IDefense {
    List<Ship> startGame(int[] ship_sizes);
    void shot(Point p);
    void endGame();
  }
}
