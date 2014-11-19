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
        public static Joueur temp1 = null;
        public static Joueur temp2 = null;
        public static string messageEnnemis = "";
        public static int joueurDepart = 0;
        public static bool Waiting=false;

        public static List<Joueur> tabJoueur;
        public static List<Joueur> tabPartie;
        public static List<gameEtat> games;

        public static Mutex mutex;
        public static Mutex mutPartie1;
        public static Mutex mutPartie2;
        public static Mutex inGame;
        public static int NoGameCourant;

        static void Main(string[] args)
        {
            mutex = new Mutex();
            mutPartie1 = new Mutex();
            mutPartie2 = new Mutex();
            inGame = new Mutex();
            Socket sck = null;
            tabJoueur = new List<Joueur>();
            tabPartie = new List<Joueur>();
            games = new List<gameEtat>();
            sck = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            sck.Bind(new IPEndPoint(0, 1234));
            sck.Listen(1000);
            Console.WriteLine("En attente de connexion");
            Thread t;

            // avoir une game
            games.Add(new gameEtat());
            NoGameCourant = games.Count - 1;

            while (true)
            {
               tabJoueur.Add(new Joueur("joueur"));
               tabJoueur[tabJoueur.Count - 1].sckJoueur = sck.Accept();              
                Console.WriteLine("Joueur connecté");               
               // Client client = new Client(tabJoueur[tabJoueur.Count - 1]);
                t = new Thread(new Client(tabJoueur[tabJoueur.Count - 1],tabJoueur.Count-1).doWork);
                t.Name = "joueur" + tabJoueur.Count;
                t.Start();   
            }
        }
        static public int getPosIndex(int pos,int posgame)
        {
            if (games[posgame].indexJoueur[0] == pos)
                return 0;
            else if (games[posgame].indexJoueur[1] == pos)
                return 1;
            else
                return -1;
        }
        static public int findEnnemis(int pos,int posGame)
        {
            if (games[posGame].indexJoueur[0] == pos)
                return games[posGame].indexJoueur[1];
            else if (games[posGame].indexJoueur[1] == pos)
                return games[posGame].indexJoueur[0];
            else
                return -1;
        }
        static public Joueur getEnnemis(int pos)
        {
            return tabJoueur[pos];
        }
        static public int AjouterJoueurPartie(int pos)
        {
            if (games[NoGameCourant].indexJoueur[0] == -1)
                games[NoGameCourant].indexJoueur[0] = pos;
            else
                games[NoGameCourant].indexJoueur[1] = pos;
            int numPartie = NoGameCourant;
            if (partieComplete(NoGameCourant))
                Serveur.NouvelleGame();

            return numPartie;
        }
        static public void enleverJoueurPartie(int pos)
        {
            if (games[NoGameCourant].indexJoueur[0] == pos)
                games[NoGameCourant].indexJoueur[0] = -1;
            else if (games[NoGameCourant].indexJoueur[1] == pos)
                games[NoGameCourant].indexJoueur[1] = -1;            
        }
        static public bool partieComplete(int posgame)
        {
            return games[posgame].indexJoueur[0] != -1 && games[posgame].indexJoueur[1] != -1;
        }
        static public void NouvelleGame()
        {
            // Verifier si on démarre la game
            // ou si on essaye d'en creer une pour rien    -  2ieme joueur
            if (games[NoGameCourant].indexJoueur[0] != -1 && games[NoGameCourant].indexJoueur[1] != -1)
            {
                games[NoGameCourant].PartieDemarre = true;
                int inactive = trouverPartieInactive();
                //une était inactive
                if (inactive != -1)
                {
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
        static private int trouverPartieInactive()
        {
            for(int i=0;i<games.Count;++i)
            {
                if (games[i].inactif)
                    return i;
            }
            return -1;
        }

    }
    class gameEtat
    {
        public bool PuCarte;
        public bool PartieDemarre;
        public bool inactif;
        public int[] indexJoueur;
        public int rand;

        public gameEtat()
        {
            inactif = false;
            PuCarte = false;
            PartieDemarre = false;
            indexJoueur = new int[2];
            indexJoueur[0] = -1;
            indexJoueur[1] = -1;
            rand = -1;
        }

        public void TerminerGame()
        {
            inactif = true;
        }
        public void ActiverGame()
        {
            inactif = false;            
            PuCarte = false;
            PartieDemarre = false;
            indexJoueur[0] = -1;
            indexJoueur[1] = -1;
        }
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
