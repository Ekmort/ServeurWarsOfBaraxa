using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Runtime.Serialization.Formatters.Binary;
using System.Text;
using System.Threading.Tasks;
using Oracle.DataAccess.Client;
using WarsOfBaraxaBD;
using warsofbaraxa;

namespace ServeurWarsOfBaraxa
{
    class Serveur
    {
        static AccesBD acces = new AccesBD();
        static private OracleConnection conn;
        static private String connexionChaine;
        private static OracleDataReader dataReader;
        static Socket sck;
        static Socket client1 = null;
        static Socket client2 = null;
        static void Main(string[] args)
        {
            sck = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            sck.Bind(new IPEndPoint(0, 1234));
            sck.Listen(1000);

            Console.WriteLine("En attente de connexion");

            while (true)
            {
                acces.Connection();
                Carte[] CarteJoueur = acces.ListerDeckJoueur("ekmort",1);
                ////if car si il se deconnecte avant de partir la partis il ne va pas aprtier la partis et revenir ici, donc le if permet de savoir quel client a été deconnect
                //if (client1 == null)
                //{
                //    client1 = sck.Accept();
                //    Console.WriteLine("Joueur1 connecté");
                //}
                //if (client2 == null)
                //{
                //    client2 = sck.Accept();
                //    Console.WriteLine("Joueur2 connecté");
                //}
                //if (client1 != null && client2 != null)
                //{
                //    //envoye au 2 clients que la parti est commencée
                //    sendClient(client1, "La partie est commencee");
                //    sendClient(client2, "La partie est commencee");
                //}
                conn.Close();
            }
        }

        static private void sendDeckJoueurClient()
        {
            acces.Connection();
            Carte[] CarteJoueur = acces.ListerDeckJoueur("ekmort", 1);
            Deck DeckJoueur = new Deck(CarteJoueur);
            sck = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            IPEndPoint localEndPoint = new IPEndPoint(IPAddress.Parse("172.17.104.127"), 1234);
            try
            {
                sck.Connect(localEndPoint);
            }
            catch
            {
                System.Console.Write("Erreur de connexion");
            }

            if (sck.Connected)
            {
                byte[] data;
                BinaryFormatter b = new BinaryFormatter();
                using (var stream = new MemoryStream())
                {
                    b.Serialize(stream, DeckJoueur);
                    data = stream.ToArray();
                }
                sck.Send(data);
            }
        }
    }
}
