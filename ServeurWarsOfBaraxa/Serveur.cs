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
using warsofbaraxa;

namespace ServeurWarsOfBaraxa
{
    class Serveur
    {
        static private OracleConnection conn;
        static private String connexionChaine;
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
                //AccesBD acces = new AccesBD();
                Connection();
                Carte[] CarteJoueur = ListerDeckJoueur("ekmort", 1);
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
            Connection();
            Carte[] CarteJoueur = ListerDeckJoueur("ekmort", 1);
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

        static public void Connection()
        {
            String serveur = "(DESCRIPTION = (ADDRESS_LIST = (ADDRESS = (PROTOCOL = TCP)(HOST = 172.17.104.127)"
            + "(PORT = 1521)))(CONNECT_DATA =(SERVICE_NAME = ORCL)))";
            connexionChaine = "data source=" + serveur + ";user id=WarsOfBaraxa;password=WarsOfBaraxa";
            conn = new OracleConnection(connexionChaine);
            conn.Open();
        }

        static public Carte[] ListerDeckJoueur(String NomJoueur, int NoDeck)
        {
            Carte[] CarteJoueur = new Carte[40];
            string sql = "SELECT NomCarte,TypeCarte,NVL(Habilete,'null'),Ble,Bois,Gem,C.NoCarte,NombreDeFois FROM CARTE C " +
            "INNER JOIN DeckCarte CD ON C.NoCarte=CD.NoCarte " +
            "INNER JOIN DECK D ON CD.NoDeck=D.NoDeck " +
            "INNER JOIN DECKJOUEUR DJ ON D.NoDeck=DJ.NoDeck WHERE DJ.IdJoueur='" + NomJoueur + "' AND DJ.NoDeck=" + NoDeck;
            OracleCommand commandeOracle = new OracleCommand(sql, conn);

            try
            {
                OracleDataReader dataReader = commandeOracle.ExecuteReader();

                if (dataReader.HasRows)
                {
                    int i = 0;
                    while (dataReader.Read())
                    {
                        for (int j = 0; j < dataReader.GetInt32(7); ++j)
                        {
                            CarteJoueur[i] = new Carte(dataReader.GetString(0), dataReader.GetString(1), dataReader.GetString(2), dataReader.GetInt32(3), dataReader.GetInt32(4), dataReader.GetInt32(5));
                            if (dataReader.GetString(1) == "Permanents")
                            {
                                string sqlPerm = "SELECT TypePerm,Attaque,Armure,Vie FROM Permanents WHERE NoCarte=" + dataReader.GetInt32(6);
                                OracleCommand commandeOraclePerm = new OracleCommand(sqlPerm, conn);
                                OracleDataReader dataReaderPerm = commandeOraclePerm.ExecuteReader();
                                CarteJoueur[i].perm = new Permanent(dataReaderPerm.GetString(0), dataReaderPerm.GetInt32(1), dataReaderPerm.GetInt32(2), dataReaderPerm.GetInt32(3));
                            }
                            ++i;
                        }
                    }
                }
            }
            catch (InvalidOperationException e)
            {
                Console.Write(e);
            }
            return CarteJoueur;
        }
        public bool estPresent(string nomAlias, string mdp)
        {
            string sql = "select * from joueur where IdJoueur='" + nomAlias + "' and Pword='" + mdp + "'";
            OracleCommand orac = new OracleCommand(sql, conn);
            OracleDataReader dataReader = orac.ExecuteReader();
            if (dataReader.HasRows)
            {
                return true;
            }
            return false;
        }
        public bool estDejaPresent(string nomAlias)
        {
            string sql = "select * from joueur where IdJoueur='" + nomAlias;
            OracleCommand orac = new OracleCommand(sql, conn);
            OracleDataReader dataReader = orac.ExecuteReader();
            if (dataReader.HasRows)
            {
                return true;
            }
            return false;
        }
    }
}
