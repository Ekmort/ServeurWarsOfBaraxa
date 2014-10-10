using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Runtime.Serialization.Formatters.Binary;
using System.Text;
using System.Threading;
using WarsOfBaraxaBD;
using warsofbaraxa;

namespace ServeurWarsOfBaraxa
{
    class Serveur
    {
        static Socket sck;
        static Socket client1 = null;
        static void Main(string[] args)
        {
            sck = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            sck.Bind(new IPEndPoint(0, 1234));
            sck.Listen(1000);

            Console.WriteLine("En attente de connexion");

            while (true)
            {
                if (client1 == null)
                {
                    client1 = sck.Accept();
                    Console.WriteLine("Joueur connecté");
                    Client client = new Client(client1);
                    Thread t = new Thread(client.doWork);
                    t.Start();
                }
                client1 = null;
            }
        }
    }
}
