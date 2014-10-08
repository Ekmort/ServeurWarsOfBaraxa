using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using warsofbaraxa;
using WarsOfBaraxaBD;

namespace ServeurWarsOfBaraxa
{
    class Serveur
    {
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
                //if car si il se deconnecte avant de partir la partis il ne va pas aprtier la partis et revenir ici, donc le if permet de savoir quel client a été deconnect
                if (client1 == null)
                {
                    client1 = sck.Accept();
                    Console.WriteLine("Joueur1 connecté");
                }
                if (client2 == null)
                {
                    client2 = sck.Accept();
                    Console.WriteLine("Joueur2 connecté");
                }
                if (client1 != null && client2 != null)
                {
                    //envoye au 2 clients que la parti est commencée
                    sendClient(client1, "La partie est commencee");
                    sendClient(client2, "La partie est commencee");
                }
            }
        }

        private Carte[] sendClientDeckJoueur
        {

        }
    }
}
