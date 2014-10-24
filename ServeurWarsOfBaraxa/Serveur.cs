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
        public static int joueurDepart = 0;

        public static List<Joueur> tabJoueur;
        public static List<Joueur> tabPartie;
        public static Mutex mutex;
        static void Main(string[] args)
        {
            mutex = new Mutex();
            Socket sck = null;
            tabJoueur = new List<Joueur>();
            tabPartie = new List<Joueur>();
            sck = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            sck.Bind(new IPEndPoint(0, 1234));
            sck.Listen(1000);
            Console.WriteLine("En attente de connexion");
            Thread t;
            while (true)
            {
               tabJoueur.Add(new Joueur("joueur"));
               tabJoueur[tabJoueur.Count - 1].sckJoueur = sck.Accept();              
                Console.WriteLine("Joueur connecté");               
               // Client client = new Client(tabJoueur[tabJoueur.Count - 1]);
                t = new Thread(new Client(tabJoueur[tabJoueur.Count - 1]).doWork);
                t.Name = "joueur" + tabJoueur.Count;
                t.Start();   
            }
        }
    }
}
