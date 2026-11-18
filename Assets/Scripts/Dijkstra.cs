using System.Collections.Generic;
using UnityEngine;

// Implémentation manuelle de Dijkstra sur une grille (sans NavMesh ni bibliothèque).
// cost[x,y] = coût pour ENTRER dans la case (x,y). -1 = mur (infranchissable).
public static class Dijkstra
{
    // Les 4 directions autorisées (pas de diagonales)
    static readonly Vector2Int[] Dirs =
    {
        Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right
    };

    // Renvoie le chemin de start à goal (les deux inclus), ou null si goal est inaccessible
    // totalCost = cout total du chemin (-1 si aucun chemin)
    public static List<Vector2Int> FindPath(int[,] cost, Vector2Int start, Vector2Int goal, out int totalCost)
    {
        int w = cost.GetLength(0), h = cost.GetLength(1);

        int[,] dist = new int[w, h];            // distance minimale connue depuis start
        Vector2Int[,] prev = new Vector2Int[w, h]; // prédécesseur de chaque case (pour reconstruire le chemin)
        bool[,] done = new bool[w, h];          // cases dont la distance est définitive

        // Init: toutes les distances à l'infini, aucun prédécesseur
        for (int x = 0; x < w; x++)
            for (int y = 0; y < h; y++)
            {
                dist[x, y] = int.MaxValue;
                prev[x, y] = new Vector2Int(-1, -1);
            }

        dist[start.x, start.y] = 0; // on part de la case du monstre

        while (true)
        {
            // Choisir la case non visitée avec la plus petite distance
            // (balayage du tableau)
            Vector2Int cur = new Vector2Int(-1, -1);
            int best = int.MaxValue;
            for (int x = 0; x < w; x++)
                for (int y = 0; y < h; y++)
                    if (!done[x, y] && dist[x, y] < best)
                    {
                        best = dist[x, y];
                        cur = new Vector2Int(x, y);
                    }

            if (cur.x < 0) break;      // plus aucune case atteignable
            if (cur == goal) break;    // chemin le plus court vers le joueur trouvé
            done[cur.x, cur.y] = true;

            // On regarde les 4 voisins de la case courante
            foreach (var d in Dirs)
            {
                var n = cur + d;
                // Ignorer les cases hors de la grille
                if (n.x < 0 || n.y < 0 || n.x >= w || n.y >= h) continue;
                // Ignorer les murs et les cases déjà finalisées
                if (cost[n.x, n.y] < 0 || done[n.x, n.y]) continue;

                // Nouveau cout = cout pour arriver à cur + coût d'entrée dans le voisin
                int nd = dist[cur.x, cur.y] + cost[n.x, n.y];
                if (nd < dist[n.x, n.y])
                {
                    dist[n.x, n.y] = nd; // meilleur chemin trouvé pour ce voisin
                    prev[n.x, n.y] = cur; // on retient d'où on vient
                }
            }
        }

        totalCost = dist[goal.x, goal.y];
        if (totalCost == int.MaxValue) { totalCost = -1; return null; } // aucun chemin

        // Reconstruction du chemin à l'envers grâce au tableau des prédécesseurs
        var path = new List<Vector2Int>();
        for (var p = goal; p.x >= 0; p = prev[p.x, p.y])
        {
            path.Add(p);
            if (p == start) break;
        }
        path.Reverse(); // on remet dans le sens start -> goal
        return path;
    }
}