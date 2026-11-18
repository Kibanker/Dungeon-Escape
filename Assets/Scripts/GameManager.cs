using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

// Les sprites sont optionnels : si un champ est vide, une case colorée est utilisée à la place.
public class GameManager : MonoBehaviour
{
    // ---------- SPRITES (à glisser dans l'Inspector) ----------
    // Les sprites sont optionnels : si un champ est vide, une case colorée est utilisée à la place
    // Je n'étais pas sûr d'avoir le temps de faire un jeu "joli" donc je les ai laissé optionnels
    [Header("Sprites (optionnels, sinon couleur par défaut)")]
    public Sprite floorSprite;
    public Sprite wallSprite;
    public Sprite mudSprite;
    public Sprite dangerSprite;
    public Sprite exitSprite;
    public Sprite keySprite;
    public Sprite playerSprite;
    public Sprite monsterSprite;

    // ---------- CARTE ----------
    // # mur | . sol (coût 1) | ~ boue (coût 3) | ^ danger (coût 5)
    // P joueur | M monstre | K clé | E sortie
    readonly string[] map =
    {
        "############",
        "#P...#....K#",
        "#.##.#.~~#.#",
        "#.#..~.#...#",
        "#.#.###.#.##",
        "#...^^..#..#",
        "###.##.##~.#",
        "#M.....#..E#",
        "############",
    };

    // ---------- ÉTAT DU JEU ----------
    int w, h;                       // largeur / hauteur de la grille
    int[,] cost;                    // cout de chaque case (-1 = mur)
    Vector2Int player, monster, key, exit; // positions en coordonnées de grille
    bool hasKey, gameOver, won, showPath = true;
    int pathCost;                   // cout total du chemin du monstre (affiché à l'écran)

    Sprite square;                  // petit carré blanc généré par code (fallback)
    Transform playerT, monsterT, keyT;
    readonly List<GameObject> pathMarks = new List<GameObject>(); // points du chemin affichés

    void Start()
    {
        // Création d'un sprite carré blanc 1x1
        var tex = new Texture2D(1, 1);
        tex.SetPixel(0, 0, Color.white);
        tex.Apply();
        tex.filterMode = FilterMode.Point;
        square = Sprite.Create(tex, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 1f);

        h = map.Length;
        w = map[0].Length;
        cost = new int[w, h];

        // Lecture de la carte caractère par caractère
        for (int row = 0; row < h; row++)
            for (int x = 0; x < w; x++)
            {
                char c = map[row][x];
                int y = h - 1 - row; // la ligne 0 du texte est en HAUT de l'écran
                var pos = new Vector2Int(x, y);

                // Valeurs par défaut = sol normal
                Color col = new Color(0.25f, 0.22f, 0.2f);
                Sprite spr = floorSprite;
                cost[x, y] = 1;

                // Chaque type de case définit son coût, sa couleur ET son sprite
                switch (c)
                {
                    case '#': cost[x, y] = -1; col = new Color(0.1f, 0.1f, 0.12f); spr = wallSprite;   break;
                    case '~': cost[x, y] = 3;  col = new Color(0.2f, 0.4f, 0.7f);  spr = mudSprite;    break;
                    case '^': cost[x, y] = 5;  col = new Color(0.7f, 0.2f, 0.15f); spr = dangerSprite; break;
                    case 'E': exit = pos;      col = new Color(0.2f, 0.7f, 0.3f);  spr = exitSprite;   break;
                    case 'P': player = pos;  break; // le sol reste dessiné sous le joueur
                    case 'M': monster = pos; break;
                    case 'K': key = pos;     break;
                }
                Make(pos, spr, col, 0.98f, 0); // on dessine la case (ordre 0 = fond)
            }

        // Objets mobiles dessinés par-dessus la grille (ordre d'affichage plus élevé)
        keyT     = Make(key,     keySprite,     Color.yellow, 0.4f, 2);
        playerT  = Make(player,  playerSprite,  Color.cyan,   0.7f, 3);
        monsterT = Make(monster, monsterSprite, Color.magenta, 0.7f, 3);

        // Caméra centrée sur la carte
        var cam = Camera.main;
        cam.orthographic = true;
        cam.orthographicSize = h / 2f + 1.5f;
        cam.transform.position = new Vector3(w / 2f - 0.5f, h / 2f - 0.5f, -10);
        cam.backgroundColor = Color.black;

        UpdatePathDisplay();
    }

    // Crée un objet visuel à la position de grille p.
    // Si s est null -> carré coloré (couleur c). Sinon -> le sprite fourni (sans teinte).
    // size = taille voulue en cases (1 = une case entière), quelle que soit la taille du sprite.
    Transform Make(Vector2Int p, Sprite s, Color c, float size, int order)
    {
        var go = new GameObject("tile");
        go.transform.position = new Vector3(p.x, p.y, 0);

        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = (s != null) ? s : square;
        sr.color = (s != null) ? Color.white : c; // pas de teinte si on utilise un vrai sprite
        sr.sortingOrder = order;

        // On adapte l'échelle pour que le sprite occupe exactement "size" cases,
        // peu importe sa résolution ou son "Pixels Per Unit"
        float k = size / sr.sprite.bounds.size.x;
        go.transform.localScale = new Vector3(k, k, 1);
        return go.transform;
    }

    void Update()
    {
        // R = recommencer (on recharge la scène), Espace = afficher/cacher le chemin
        if (Input.GetKeyDown(KeyCode.R)) SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        if (Input.GetKeyDown(KeyCode.Space)) { showPath = !showPath; UpdatePathDisplay(); }
        if (gameOver) return; // plus de déplacement après victoire/défaite

        // Flèches + WASD (QWERTY) + ZQSD (AZERTY)
        if (Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.W) || Input.GetKeyDown(KeyCode.Z)) Move(Vector2Int.up);
        else if (Input.GetKeyDown(KeyCode.DownArrow) || Input.GetKeyDown(KeyCode.S)) Move(Vector2Int.down);
        else if (Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.A) || Input.GetKeyDown(KeyCode.Q)) Move(Vector2Int.left);
        else if (Input.GetKeyDown(KeyCode.RightArrow) || Input.GetKeyDown(KeyCode.D)) Move(Vector2Int.right);
    }

    // Un tour complet : joueur -> vérifications -> monstre (Dijkstra) -> collision
    void Move(Vector2Int d)
    {
        var n = player + d;
        // Refuser si on sort de la grille ou si c'est un mur
        if (n.x < 0 || n.y < 0 || n.x >= w || n.y >= h || cost[n.x, n.y] < 0) return;

        // 1. Le joueur se déplace d'une case
        player = n;
        playerT.position = new Vector3(player.x, player.y, 0);

        // Ramassage de la clé
        if (!hasKey && player == key) { hasKey = true; keyT.gameObject.SetActive(false); }
        // Victoire : clé en poche + case de sortie
        if (hasKey && player == exit) { won = true; gameOver = true; ClearPath(); return; }
        // Défaite si le joueur marche directement sur le monstre
        if (player == monster) { gameOver = true; ClearPath(); return; }

        // 2. Le monstre lance Dijkstra vers le joueur et avance d'UNE case sur le chemin
        var path = Dijkstra.FindPath(cost, monster, player, out pathCost);
        if (path != null && path.Count > 1) // path[0] = position actuelle, path[1] = prochaine case
        {
            monster = path[1];
            monsterT.position = new Vector3(monster.x, monster.y, 0);
        }

        // 3. Le monstre a-t-il attrapé le joueur ?
        if (monster == player) { gameOver = true; ClearPath(); return; }

        UpdatePathDisplay();
    }

    // Supprime les points du chemin affichés
    void ClearPath()
    {
        foreach (var g in pathMarks) Destroy(g);
        pathMarks.Clear();
    }

    // Recalcule et affiche le chemin du monstre vers le joueur (pour la démo de Dijkstra)
    void UpdatePathDisplay()
    {
        ClearPath();
        var path = Dijkstra.FindPath(cost, monster, player, out pathCost);
        if (!showPath || path == null) return;
        // On saute la première case (monstre) et la dernière (joueur)
        for (int i = 1; i < path.Count - 1; i++)
        {
            var t = Make(path[i], null, new Color(1f, 1f, 1f, 0.45f), 0.5f, 1);
            pathMarks.Add(t.gameObject);
        }
    }

    // Interface simple (texte à l'écran)
    void OnGUI()
    {
        GUI.skin.label.fontSize = 18;
        GUI.Label(new Rect(10, 10, 600, 30), "Clé : " + (hasKey ? "oui" : "non") + "   Coût du chemin du monstre : " + pathCost);
        GUI.Label(new Rect(10, 36, 700, 30), "Déplacement : flèches / WASD / ZQSD   Espace : afficher/cacher le chemin   R : recommencer");
        GUI.Label(new Rect(10, 62, 700, 30), "Bleu = boue (3)   Rouge = danger (5)   Vert = sortie   Jaune = clé");

        // Écrans de fin
        if (gameOver)
        {
            GUI.skin.label.fontSize = 36;
            GUI.Label(new Rect(Screen.width / 2f - 250, Screen.height / 2f - 30, 600, 60),
                won ? "VOUS VOUS ÊTES ÉCHAPPÉ ! (R pour rejouer)" : "ATTRAPÉ ! (R pour réessayer)");
        }
    }
}