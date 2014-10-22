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
        static Socket sck;
        static Socket client1 = null;
        public static Joueur temp1 = null;
        public static Joueur temp2 = null;
        public static List<Joueur> tabJoueur;
        public static List<Joueur> tabPartie;
        static void Main(string[] args)
        {
            tabJoueur = new List<Joueur>();
            tabPartie = new List<Joueur>();
            sck = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            sck.Bind(new IPEndPoint(0, 1234));
            sck.Listen(1000);
            Console.WriteLine("En attente de connexion");

            while (true)
            {
                    client1 = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                    client1 = sck.Accept();
                    Joueur temp = new Joueur("joueur",client1);
                    SocketInformation socketInfo = client1.DuplicateAndClose(Process.GetCurrentProcess().Id);
                    temp.sckJoueur = new Socket(socketInfo);
                    tabJoueur.Add(temp);
                    int pos = tabJoueur.IndexOf(temp);
                    Console.WriteLine("Joueur connecté");
                    Client client = new Client(temp,pos);
                    Thread t = new Thread(client.doWork);
                    t.Start();
            }
        }
    }
}
