using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Runtime.Serialization.Formatters.Binary;
using System.Text;
using System.Diagnostics;
using System.Threading;
using WarsOfBaraxaBD;
using warsofbaraxa;

namespace ServeurWarsOfBaraxa
{

    class Serveur
    {
        //tab qui contient tout les joueurs du serveur
        public static List<Joueur> tabJoueur;
        //tab qui contient les parties
        public static List<gameEtat> games;
        //les mutex utilise a differente place
        public static Mutex mutex;
        public static Mutex mutPartie1;
        public static Mutex mutPartie2;
        public static Mutex inGame;
        // numero de la prochain partie qui va etre joué
        public static int NoGameCourant;

        static void Main(string[] args)
        {
            ////////////////----------initialisation----------------///////////////////////////////
            mutex = new Mutex();
            mutPartie1 = new Mutex();
            mutPartie2 = new Mutex();
            inGame = new Mutex();
            Socket sck = null;
            tabJoueur = new List<Joueur>();
            games = new List<gameEtat>();
            sck = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            sck.Bind(new IPEndPoint(0, 50054));
            sck.Listen(1000);
            Console.WriteLine("En attente de connexion");
            Thread t;
            /////////////////////--------------------------------------/////////////////////////////////////////////////

            // avoir une premier partie de prêts
            games.Add(new gameEtat());
            NoGameCourant = games.Count - 1;
    
            while (true)
            {
                //trouve une position pour le joueur
                int numpossible= trouverPlaceJoueur();
                //si -1 alors il n'y a pa de joueur qui sont partie il faut donc crée une nouvelle palce
               if(numpossible ==-1)
               {
                   tabJoueur.Add(new Joueur("joueur"));
                   numpossible = tabJoueur.Count - 1;
                   tabJoueur[numpossible].sckJoueur = sck.Accept();  
               }
               else
               {
                    tabJoueur[numpossible] = new Joueur("joueur");
                    tabJoueur[numpossible].sckJoueur = sck.Accept();
               }               
                //création du thread du client pour qu'il soit connecter au serveur et faire ses actions
                t = new Thread(new Client(tabJoueur[numpossible],numpossible).doWork);
                t.Name = "joueur" + tabJoueur.Count;
                t.Start();   
            }
        }
        //trouve la place du joueur dans le tableau de joueur
        static public int trouverPlaceJoueur()
        {
            //recherchesi il y a une place vide
            for (int i = 0; i < tabJoueur.Count; ++i)
            {
                //si oui il retourne la position
                if (tabJoueur[i] == null)
                    return i;
            }
            //si non il retourne -1
            return -1;
        }
        //retourne la position dans la partie(soit 0 ou 1) d'un joueur selon le numero de partie et la position du joueur dans le tableau de joueur
        static public int getPosIndex(int pos,int posgame)
        {
            //si les positions sont logique
            if (posgame != -1 && pos != -1)
            {
                //on vérifie si la position du joueur est a 0 ou 1
                //si 0 on retourne 0 si 1 on retourne 1
                if (games[posgame].indexJoueur[0] == pos)
                    return 0;
                else if (games[posgame].indexJoueur[1] == pos)
                    return 1;
            }
            //si il n'est pas 0 ou 1 alors il n'est pas dans la partie on retourne donc -1
                return -1;
        }
        //retourne le numero de joueur de l'ennemis selon le numero de partie et le numero du joueur
        static public int findEnnemis(int pos,int posGame)
        {
            //si les positions sont logique
            if (pos != -1 && posGame != -1)
            {
                //si on est joueur 0 on retourne ;'index du joeuur 1 sinon on retourne l'index du joeuur 0 si on a le bon index(si la pos du joeuur = l'index du joueur a la position 1)
                if (games[posGame].indexJoueur[0] == pos)
                    return games[posGame].indexJoueur[1];
                else if (games[posGame].indexJoueur[1] == pos)
                    return games[posGame].indexJoueur[0];
            }
            //s'il le numero n'est pas dans la partie on retourne -1
            return -1;
        }
        //Retourne le joueur
        static public Joueur getJoueur(int pos)
        {
            //si il est dans le tableau de joueur on retourne le joueur sinon on retourne null
            if(pos != -1)
                return tabJoueur[pos];

            return null;
        }
        //ajoute un joueur a une partie et si la partie est pleine elle crée une nouvelle partie
        static public int AjouterJoueurPartie(int pos)
        {
            //le place soit a la pos 0 ou 1 selon le nombre de joueur dans la partie(si il y a quelqun a la pos 0 déjà)
            if (games[NoGameCourant].indexJoueur[0] == -1)
                games[NoGameCourant].indexJoueur[0] = pos;
            else
                games[NoGameCourant].indexJoueur[1] = pos;
            //on retourne le num de partie pour que les joueur sachent dans quelle parties ils sont
            int numPartie = NoGameCourant;
            //si la game est pleine on créer une nouvelle partie
            if (partieComplete(NoGameCourant))
                Serveur.NouvelleGame();

            return numPartie;
        }
        //enleve un joueur d'une partie (si il décide de quitter pendant le log in)
        static public void enleverJoueurPartie(int pos)
        {
            //vérifie il est ou dans la partie courante et met sa position a -1
            if (games[NoGameCourant].indexJoueur[0] == pos)
                games[NoGameCourant].indexJoueur[0] = -1;
            else if (games[NoGameCourant].indexJoueur[1] == pos)
                games[NoGameCourant].indexJoueur[1] = -1;            
        }
        //vérifie si la partie est pleine
        static public bool partieComplete(int posgame)
        {
            return games[posgame].indexJoueur[0] != -1 && games[posgame].indexJoueur[1] != -1;
        }
        //lance une nouvelle partie
        static public void NouvelleGame()
        {
            // Verifier si on démarre la game
            // ou si on essaye d'en creer une pour rien    -  2ieme joueur
            if (games[NoGameCourant].indexJoueur[0] != -1 && games[NoGameCourant].indexJoueur[1] != -1)
            {
                //la partie est complete donc on part la game qu'il y a 2 joueur de dans
                games[NoGameCourant].PartieDemarre = true;
                //vérifie si il y a une partie inactive
                int inactive = trouverPartieInactive();
                //une était inactive
                if (inactive != -1)
                {
                    //on active la partie
                    NoGameCourant = inactive;
                    games[inactive].ActiverGame();
                }
                //créé une nouvelle game
                else
                {
                    games.Add(new gameEtat());
                    NoGameCourant = games.Count - 1;
                }
            }
        }
        //trouve si il y a une partie inactive
        static private int trouverPartieInactive()
        {
            for(int i=0;i<games.Count;++i)
            {
                //si il y a une partie inactive donc on retourne le numPartie inactif
                if (games[i].inactif)
                    return i;
            }
            //sinon -1
            return -1;
        }
        //si l'ennemis part on met la sa variable a true
        static public void ennemiPart(int numjoueur,int numPartie)
        { 
            if(numjoueur==0)
                games[numPartie].joueurpart[1] =true;
            else if(numjoueur ==1)
                games[numPartie].joueurpart[0] = true;
        }
        //si on part on met notre variable a true
        static public void JoueurPart(int numjoueur, int numPartie)
        {
            if(numPartie ==0 || numPartie==1)
                games[numPartie].joueurpart[numjoueur] = true;
        }

    }
    //classe de partie il contient les variables d'états
    class gameEtat
    {
        //si un joeuur n'a plus de carte
        public bool PuCarte;
        public bool PartieDemarre;
        public bool inactif;
        //le numero de joueur des 2 joueurs dans la partie
        public int[] indexJoueur;
        //le numero(random) du joueur de départ
        public int rand;
        //pour savoir si un des 2 joueurs part de la partie
        public bool[] joueurpart;
        //mutex entre les 2 joueurs
        public Mutex mutpartie;
        public gameEtat()
        {
            //------initialisation
            mutpartie = new Mutex();
            inactif = false;
            PuCarte = false;
            PartieDemarre = false;
            indexJoueur = new int[2];
            indexJoueur[0] = -1;
            indexJoueur[1] = -1;
            joueurpart = new bool[2];
            joueurpart[0] = false;
            joueurpart[1] = false;
            rand = -1;
        }
        //termine la game(la met a inactif)
        public void TerminerGame()
        {
            inactif = true;
        }
        //active une game
        public void ActiverGame()
        {
            //reset les valeurs a ces valeurs de base
            inactif = false;            
            PuCarte = false;
            PartieDemarre = false;
            indexJoueur[0] = -1;
            indexJoueur[1] = -1;
            joueurpart[0] = false;
            joueurpart[1] = false;
            rand = -1;

        }
        //trouve le joueur de départ
        public int setRandom()
        {
            if (rand == -1)
            {
                Random r = new Random();
                rand=r.Next(1, 3);
            }
            return rand;
        }
        
    }
}
