using System;
using System.Data;
using System.Text;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.IO;
using Oracle.DataAccess.Client;
using System.Net;
using System.Net.Sockets;
using System.Runtime.Serialization.Formatters.Binary;
using warsofbaraxa;
using WarsOfBaraxaBD;

namespace ServeurWarsOfBaraxa
{
    class Client
    {
        static private Joueur Moi;
        static private Joueur Ennemis; 
        static AccesBD acces;
        static private OracleConnection conn;
        static private String connexionChaine;
        private static OracleDataReader dataReader;
        private static bool Deconnection = false;
        private static bool partieCommencer = false;
        private static int posClient;
        public Client(Joueur temp,int pos)
        {
            Moi= new Joueur(temp.nom,temp.sckJoueur);
            Deconnection = false;
            partieCommencer = false;
            posClient=pos;
        }
        public void doWork()
        {
            acces = new AccesBD();
            acces.Connection();
            while (!Deconnection)
            {
                if (!partieCommencer)
                {
                    string message=recevoirResultat(Moi.sckJoueur);
                    string[] data= message.Split(new char[]{','});
                    TraiterMessageAvantPartie(data);
                }
                else
                {
                    if (aPerdu(Moi))
                    {
                        partieCommencer = false;
                        sendClient(Moi.sckJoueur,"vous avez perdu");
                        acces.AjouterDefaite(Moi.nom);
                    }
                    else if (aPerdu(Ennemis))
                    {
                        sendClient(Moi.sckJoueur, "vous avez gagné");
                        acces.AjouterVictoire(Moi.nom);
                        partieCommencer = false;
                    }
                }
            }
        }
        static private void TraiterMessageAvantPartie(string[] data)
        {
            switch (data.Length)
            {
                case 2:
                if(data[0] == "afficher profil")
                {
                    sendProfil(data[1]);
                }
                else if (estPresent(data))
                   Moi.nom = data[0];
                break;
                case 4:
                    if(peutEtreAjouter(data))
                    Moi.nom=data[0];
                break;
            }
            switch (data[0])
            { 
                case "deconnection":
                    Deconnection = true;
                    Serveur.tabJoueur.Remove(Moi);
                break;
                case "recevoir Deck":
                    sendDeck(Moi.nom);
                break;
                case "trouver partie":
                if(startGame(Moi.sckJoueur, posClient))
                partieCommencer = true;
                break;
            }
        }
        static private bool startGame(Socket client, int pos)
        {
            Serveur.tabPartie.Add(Moi);
            bool rechercher = true;
            while (rechercher && Serveur.tabPartie.Count<2)
            {
                sendClient(Moi.sckJoueur, "recherche");
                string message = recevoirResultat(Moi.sckJoueur);
                if (message == "deconnection")
                {
                    rechercher = false;
                    Deconnection = true;
                    Serveur.tabJoueur.Remove(Moi);
                    Serveur.tabPartie.Remove(Moi);
                } 
            }
            if (rechercher)
            {
                if (Serveur.temp1 == null)
                    Serveur.temp1 = new Joueur(Serveur.tabPartie[0].nom, Serveur.tabPartie[0].sckJoueur);
                if (Serveur.temp2 == null)
                    Serveur.temp2 = new Joueur(Serveur.tabPartie[1].nom, Serveur.tabPartie[1].sckJoueur);
                if (Serveur.temp1.nom == Serveur.tabPartie[0].nom)
                    Ennemis = new Joueur(Serveur.tabPartie[1].nom, Serveur.tabPartie[1].sckJoueur);
                else
                    Ennemis = new Joueur(Serveur.tabPartie[0].nom, Serveur.tabPartie[0].sckJoueur);

                Serveur.tabPartie.Remove(Moi);
                if (Ennemis != null)
                {
                    sendClient(Moi.sckJoueur, "Partie Commencer,contre joueur: " + Ennemis.sckJoueur.LocalEndPoint);
                    return true;
                }
                else
                {
                    startGame(client, pos);
                }
            }
            else
            {
                return false;
            }
            return true;
        }
        static private bool estPresent(string[] data)
        {
            if (acces.estPresent(data[0], data[1]) && !estConnecter(data[0]))
            {
                sendClient(Moi.sckJoueur, "oui");
                return true;
            }
            else
            {
                sendClient(Moi.sckJoueur, "non");
                return false;
            }        
        }
        static private bool estConnecter(string alias)
        {
            for (int i = 0; i < Serveur.tabJoueur.Count; ++i)
            {
                if (Serveur.tabJoueur[i].nom == alias)
                    return true;
            }
            return false;
        }
        static private bool peutEtreAjouter(string[] data)
        {
            if (acces.estDejaPresent(data[0]))
            {
                sendClient(Moi.sckJoueur, "oui");
                return false;
            }
            else
            {
                sendClient(Moi.sckJoueur, "non");
                acces.ajouter(data[0], data[1], data[2], data[3]);
                return true;

            }        
        }
        private void sendDeckJoueurClient(String User)
        {
            acces.Connection();
            Carte[] CarteJoueur = acces.ListerDeckJoueur(User,1);
            Deck DeckJoueur = new Deck(CarteJoueur);
            //sck = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            IPEndPoint localEndPoint = new IPEndPoint(IPAddress.Parse("172.17.104.127"), 1234);
            try
            {
                //sck.Connect(localEndPoint);
            }
            catch
            {
                System.Console.Write("Erreur de connexion");
            }

            /*if (sck.Connected)
            {
                byte[] data;
                BinaryFormatter b = new BinaryFormatter();
                using (var stream = new MemoryStream())
                {
                    b.Serialize(stream, DeckJoueur);
                    data = stream.ToArray();
                }
                sck.Send(data);
            }*/
        }
        private static string recevoirResultat(Socket client)
        {
            string strData = "";
            try
            {
                byte[] buff = new byte[client.SendBufferSize];
                int bytesRead = Moi.sckJoueur.Receive(buff);
                byte[] formatted = new byte[bytesRead];
                for (int i = 0; i < bytesRead; i++)
                {
                    formatted[i] = buff[i];
                }
                strData = Encoding.ASCII.GetString(formatted);
            }
            //ne fait rien car le client n'a rien envoyer parcequ'il c'est déconnecter
            catch (SocketException sock) { }
            return strData;
        }
        private static void sendClient(Socket client, String text)
        {
            try
            {
                byte[] data = Encoding.ASCII.GetBytes(text);
                client.Send(data);
            }
            catch { Console.Write("Erreur de telechargement des donnees"); }
        }
        private static void sendProfil(string alias)
        {
            string profile=acces.getProfil(alias);
            sendClient(Moi.sckJoueur, profile);
        }
        private static bool aPerdu(Joueur player)
        { 
            if(player.vie<=0 || player.nbCarteDeck <=0)
            {
                return true;
            }
            return false;
        }
        private static void sendDeck(string alias)
        {
            string deck = acces.getDeckJoueur(alias);
            if (deck != "")
                sendClient(Moi.sckJoueur, deck);
            else
                sendClient(Moi.sckJoueur, "aucun deck");
        }
    }
}
