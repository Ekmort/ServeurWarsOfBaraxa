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
        private Joueur Moi;
        private Deck monDeck;
        private Joueur Ennemis;
        private Thread t;
        ThreadLire ReceiveMessage;
        AccesBD acces;
        private bool Deconnection = false;
        private bool partieCommencer = false;
        bool Debut = true;
        private int posPartie;
        private int posClient;
        private string NomDeck="";
        public Client(Joueur temp,int posT)
        {
            posPartie = -1;
            Moi = temp;
            Deconnection = false;
            partieCommencer = false;
            posClient = posT;
            ReceiveMessage = new ThreadLire();
            ReceiveMessage.workSocket = Moi.sckJoueur;
          
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
                    if(Debut)
                    AvantMatch();

                    if (Moi.Depart)
                    {
                        if (t != null && t.IsAlive)
                        {
                            t.Abort();
                        }
                        Tour();
                    }
                    else
                    {
                        if (t == null || !t.IsAlive)
                        {
                            t = new Thread(ReceiveMessage.doWork);
                            t.Start();
                        }
                        else
                        {
                            if (ReceiveMessage.message != "")
                            {
                                traiterMessagePartie(ReceiveMessage.message.Split(new char[] { '.' }));
                                ReceiveMessage.message = "";
                            }
                        }
                        verifiervictoireEnnemis();
                    }
                }
            }
        }
        private bool verifierVictoire()
        {
            if (Moi != null)
            {
                if (aPerdu(Moi) || Serveur.games[posPartie].PuCarte)
                {
                    partieCommencer = false;
                    acces.AjouterDefaite(Moi.nom);
                    Serveur.games[posPartie].TerminerGame();
                    resetPartie();
                    return true;
                }
                
            }
            if (Ennemis != null)
            {
                if (aPerdu(Ennemis))
                {
                    acces.AjouterVictoire(Moi.nom);
                    Serveur.games[posPartie].TerminerGame();
                    resetPartie();
                    partieCommencer = false;
                    return true;
                }
            }
            bool estPartie = JoueurPartie();
            if (estPartie)
                return true;

            return false;
        }
        private bool JoueurPartie()
        { 
                int num = Serveur.getPosIndex(posClient, posPartie);
                int numEnnemi = Serveur.findEnnemis(posClient, posPartie);
                if (num != -1 && numEnnemi != -1)
                {
                    if (Serveur.games[posPartie].joueurpart[num])
                    {
                        acces.AjouterDefaite(Moi.nom);
                        Serveur.games[posPartie].TerminerGame();
                        resetPartie();
                        partieCommencer = false;
                        return true;
                    }
                    else if (Serveur.games[posPartie].joueurpart[numEnnemi])
                    {
                        acces.AjouterVictoire(Moi.nom);
                        Serveur.games[posPartie].TerminerGame();
                        resetPartie();
                        partieCommencer = false;
                        return true;
                    }
                }
                return false;
        }
        private void resetPartie()
        {
            posPartie = -1;
            NomDeck="";
            Moi.vie = 30;
            Ennemis.vie = 30;
            monDeck = null;
            Ennemis = null;
            Debut = true;

        }
        private void verifiervictoireEnnemis()
        {
            if (Moi != null && Ennemis != null)
            {
                if (aPerdu(Moi))
                {
                    partieCommencer = false;
                    acces.AjouterDefaite(Moi.nom);
                    Serveur.games[posPartie].TerminerGame();
                    resetPartie();
                }
                else if (aPerdu(Ennemis) || Serveur.games[posPartie].PuCarte)
                {
                    acces.AjouterVictoire(Moi.nom);
                    partieCommencer = false;
                    Serveur.games[posPartie].TerminerGame();
                    resetPartie();
                }
                JoueurPartie();
            }
        }
        //trouve le joueur et lui permet de mulligan(pas encore fait le mulligan)
        private void AvantMatch()
        {
            sendDeckJoueurClient();
            Thread.Sleep(3000);
            getFirstPlayer();
            Debut = false;
        }
        //pige une carte et apres fait les trigger de carte si il y en a(rien de faite)
        private void Tour()
        {
            bool FinTour = false;
            while (!FinTour)
            {
                string message = recevoirResultat(Moi.sckJoueur);
                    string[] data = message.Split(new char[] { '.' });
                traiterMessagePartie(data);
                FinTour = verifierVictoire();
                if (!FinTour || data[0] == "Fin De Tour")
                    FinTour = true;
            }
        }
        private bool ifHeroDmg(string[] textHabilete)
        {
            bool valide = false; int i = 0;
            while(!valide && i < textHabilete.Length)
            {
                if (textHabilete[i] == "aux" && textHabilete[i + 1] == "ennemis")
                    valide = true;
                else if (textHabilete[i] == "place" && textHabilete[i + 1] == "de" && textHabilete[i + 2] == "combat")
                    valide = true;
                i++;
            }
            return valide;
        }
        private bool ifSelfHeroDmg(string[] textHabilete)
        {
            bool valide = false; int i = 0;
            while (!valide && i < textHabilete.Length)
            {
                if (textHabilete[i] == "place" && textHabilete[i + 1] == "de" && textHabilete[i + 2] == "combat")
                    valide = true;
                i++;
            }
            return valide;
        }
        private void traiterMessagePartie(string[] data)
        {
            switch (data[0])
            { 
                case "Ajouter Mana":
                    setMana(Moi,int.Parse(data[1]),int.Parse(data[2]),int.Parse(data[3]));
                    sendClient(Ennemis.sckJoueur,"AjouterManaEnnemis."+Moi.nbBle+"."+ Moi.nbBois+"."+ Moi.nbGem);
                break;
                case "Jouer spellnotarget":
                    Carte zeSpell = ReceiveCarte(Moi.sckJoueur);
                    EnleverMana(Moi, zeSpell);
                    if (zeSpell.Habilete.Split(new char[] { ' ' })[0] == "Inflige")
                    {
                        if (ifHeroDmg(zeSpell.Habilete.Split(new char[] { ' ' })))
                            Ennemis.vie -= int.Parse(zeSpell.Habilete.Split(new char[] { ' ' })[1]);

                        if (ifSelfHeroDmg(zeSpell.Habilete.Split(new char[] { ' ' })))
                            Moi.vie -= int.Parse(zeSpell.Habilete.Split(new char[] { ' ' })[1]);
                    }
                    string spellString = SetCarteString(zeSpell);
                    sendClient(Ennemis.sckJoueur, "spellNoTarget."+data[1]+"."+spellString);
                break;
                case "Jouer spellTarget":
                    Carte zeSpelltarget = ReceiveCarte(Moi.sckJoueur);
                    EnleverMana(Moi, zeSpelltarget);
                    if (data[2] == "hero ennemis" || data[2] == "hero")
                    {
                        if (zeSpelltarget.Habilete.Split(new char[] { ' ' })[0] == "Inflige")
                        {
                            if (data[2] == "hero")
                                Moi.vie -= int.Parse(zeSpelltarget.Habilete.Split(new char[] { ' ' })[1]);
                            else
                                Ennemis.vie -= int.Parse(zeSpelltarget.Habilete.Split(new char[] { ' ' })[1]);

                            string spelltargetString = SetCarteString(zeSpelltarget);
                            sendClient(Ennemis.sckJoueur, "spellwithtarget." + data[1] +"." + data[2] + "." + spelltargetString);
                        }
                    }
                    else
                    {
                        Carte zeTarget = ReceiveCarte(Moi.sckJoueur);
                        string spelltargetString = SetCarteString(zeSpelltarget);
                        string targetString = SetCarteString(zeTarget);
                        sendClient(Ennemis.sckJoueur, "spellwithtarget." + data[1] + "." + data[2] + "." + spelltargetString + "." + targetString);
                    }
                break;
                case "Jouer Carte":
                    Carte temp = ReceiveCarte(Moi.sckJoueur);
                    EnleverMana(Moi, temp);
                    string fckingCarte = SetCarteString(temp);
                    sendClient(Ennemis.sckJoueur, "AjouterCarteEnnemis."+data[1]+"."+fckingCarte);
                break;
                case "Fin De Tour":
                    Moi.Depart = false;
                    Ennemis.Depart = true;
                    sendClient(Ennemis.sckJoueur, "Tour Commencer");
                break;

                case "Attaquer Joueur":
                    Carte attaquant = ReceiveCarte(Moi.sckJoueur);
                    setHabilete(attaquant);
                    if(attaquant.perm.estAttaquePuisante)
                        Ennemis.vie -= attaquant.perm.Attaque*2;
                    else
                        Ennemis.vie -= attaquant.perm.Attaque;
                    sendClient(Ennemis.sckJoueur, "Joueur attaquer." + Ennemis.vie.ToString());
                break;
                case "Attaquer Creature":
                    sendClient(Ennemis.sckJoueur, "Combat Creature."+data[1]+"."+data[2]+"."+data[3]+"."+data[4]
                    +"."+data[5] + "."+data[6]  +"."+data[7] +"."+data[8] +"."+data[9]  +"."+data[10]  +"."+data[11]  +"."+data[12]  +"."+data[13] + "."+data[14]  +"."+data[15]
                    +"."+data[16] + "." + data[17] + "." + data[18] + "." + data[19] + "." + data[20] + "." + data[21] + "." + data[22] + "." + data[23] + "." + data[24] + "." + data[25] + "." + data[26]);
                break;
                case "Piger":
                    sendClient(Ennemis.sckJoueur, "Ennemis pige");
                break;
                case "Carte manquante":
                    sendClient(Ennemis.sckJoueur, "Carte manquante");
                    Serveur.games[posPartie].PuCarte = true;
                break;
                case "deconnection":
                    Deconnection = true;
                    int num = Serveur.getPosIndex(posClient,posPartie);
                    if(num !=-1)
                        Serveur.JoueurPart(num,posPartie);
                    Serveur.tabJoueur[posClient] = null;
                    sendClient(Ennemis.sckJoueur, "JePart");
                break;
                case "surrender":
                    partieCommencer = false;
                    int numsurrender = Serveur.getPosIndex(posClient,posPartie);
                    if (numsurrender != -1)
                        Serveur.JoueurPart(numsurrender, posPartie);
                    sendClient(Ennemis.sckJoueur, "JePart");                    
                break;
            }

        }
        private Carte setHabilete(Carte card)
        {
            if (card.Habilete != "" && card.Habilete != null)
            {
                string[] data = card.Habilete.Split(new char[] { ',' });
                for (int i = 0; i < data.Length; ++i)
                {
                    if (card.esthabileteNormal(data[i]))
                        card.setHabileteNormal(data[i]);
                    else
                    {
                        /*set habilete special*/
                    }
                }
            }
            return card;
        }
        private string SetCarteString(Carte temp)
        {
            if (temp.perm != null)
                /*0                 1               2                   3                   4               5                   6                     7                 8                   9               10*/
                return temp.CoutBle + "." + temp.CoutBois + "." + temp.CoutGem + "." + temp.Habilete + "." + temp.TypeCarte + "." + temp.NomCarte + "." + temp.NoCarte + "." + temp.perm.Attaque + "." + temp.perm.Vie + "." + temp.perm.Armure + "." + temp.perm.TypePerm;
            else
                return temp.CoutBle + "." + temp.CoutBois + "." + temp.CoutGem + "." + temp.Habilete + "." + temp.TypeCarte + "." + temp.NomCarte + "." + temp.NoCarte;
        }
        private Carte ReceiveCarte(Socket client)
        {
            Carte carte = null;
            try
            {
                byte [] buffer = new byte[client.SendBufferSize];
                int bytesRead = client.Receive(buffer);
                byte[] formatted = new byte[bytesRead];
                BinaryFormatter receive = new BinaryFormatter();

                for (int i = 0; i < bytesRead; i++)
                {
                    formatted[i] = buffer[i];
                }
                using (var recstream = new MemoryStream(formatted))
                {
                    carte = receive.Deserialize(recstream) as Carte;
                }

            }
            catch 
            {
                Serveur.mutex.WaitOne();
                Console.Write("Erreur de telechargement des donnees");
                Serveur.tabJoueur.Remove(Moi);
                Deconnection = true;
                Serveur.mutex.ReleaseMutex();
            }
            return carte;
        }
        private void EnvoyerCarte(Socket client, Carte carte)
        {
            byte[] data;
            BinaryFormatter b = new BinaryFormatter();
            using (var stream = new MemoryStream())
            {
                b.Serialize(stream, carte);
                data = stream.ToArray();
            }
            client.Send(data); 
        }
        private void envoyerDeck(Socket client, Deck leDeck)
        {
            byte[] data;
            BinaryFormatter b = new BinaryFormatter();
            using (var stream = new MemoryStream())
            {
                b.Serialize(stream, leDeck);
                data = stream.ToArray();
            }
            client.Send(data);         
        }
        private void getFirstPlayer()
        {
            Serveur.inGame.WaitOne();
            Serveur.games[posPartie].setRandom();
            Serveur.inGame.ReleaseMutex();
            int posIndex = Serveur.getPosIndex(posClient, posPartie);
            if (Serveur.games[posPartie].rand == posIndex+1)
            {
                sendClient(Moi.sckJoueur, "Premier Joueur");
                Moi.Depart = true;
            }
            else
            {
                sendClient(Moi.sckJoueur, "Deuxieme Joueur");
                Moi.Depart = false;
            }
        }
        private void TraiterMessageAvantPartie(string[] data)
        {
            switch (data.Length)
            {
                case 2:
                if(data[0] == "afficher profil")
                {
                    sendProfil(data[1]);
                }
                else if (data[0] == "afficher profil Joueur")
                {
                    sendProfil(data[1]);
                }
                else if (estPresent(data))
                {
                    Moi.nom = data[0];
                    Console.WriteLine("Joueur connecté:" + Moi.nom + " , " + Moi.sckJoueur.RemoteEndPoint.ToString() + " , " + posClient);
                }
                break;
                case 4:
                if (peutEtreAjouter(data))
                {
                    Moi.nom = data[0];
                    ajouterBasicDeck(Moi.nom);
                    Console.WriteLine("Joueur connecté:" + Moi.nom + "," + Moi.sckJoueur.RemoteEndPoint.ToString());
                }
                break;
            }
            switch (data[0])
            { 
                case "deconnection":
                    Deconnection = true;
                    Serveur.tabJoueur[posClient] = null;
                    Moi = null;
                break;
                case "recevoir Deck":
                    sendDeck(Moi.nom);
                break;
                case "trouver partie":
                    NomDeck = data[1];
                    int numDeck=acces.getNoDeck(NomDeck);
                    if (numDeck != -1)
                    {
                        Serveur.mutPartie1.WaitOne();
                        Carte[] CarteJoueur = acces.ListerDeckJoueur(Moi.nom, numDeck);
                        Serveur.mutPartie1.ReleaseMutex();
                        monDeck = new Deck(CarteJoueur);
                    }
                    if (startGame(posClient))
                        partieCommencer = true;
                break;
                case "RetourMenu":
                    Moi.nom = "Joueur";
                break;
            }
        }
        private void ajouterBasicDeck(string alias)
        {
            acces.setBasicDeck(alias);
        }
        private bool startGame(int pos)
        {
            Serveur.mutPartie2.WaitOne();
            posPartie = Serveur.AjouterJoueurPartie(pos);
            Serveur.mutPartie2.ReleaseMutex();
            bool rechercher = true;
            Serveur.mutex.WaitOne();
            while (rechercher && !Serveur.partieComplete(posPartie))
            {
                sendClient(Moi.sckJoueur, "recherche");
                string message = recevoirResultat(Moi.sckJoueur);
                if (message == "deconnection")
                {
                    rechercher = false;
                    Deconnection = true;
                    Serveur.tabJoueur[posClient] = null;
                    Serveur.enleverJoueurPartie(pos);
                }
            }
            Serveur.mutex.ReleaseMutex();
            if (rechercher)
            {
                    setPartie();
                    if (Ennemis != null)
                    {
                        sendClient(Moi.sckJoueur, "Partie Commencer");
                        return true;
                    }
                    else
                    {
                        startGame(pos);
                    }
            }
            else
            {
                return false;
            }
            return true;
        }
        private void EnleverMana(Joueur j, Carte c)
        {
            j.nbBle -= c.CoutBle;
            j.nbBois -= c.CoutBois;
            j.nbGem -= c.CoutGem;
        }
        private void ajouterMana(Joueur j,int ble,int bois, int gem)
        {
            j.nbBle += ble;
            j.nbBois += bois;
            j.nbGem += gem;
        }
        private void setMana(Joueur j,int ble,int bois,int gem)
        {
            j.nbBle = ble;
            j.nbBois = bois;
            j.nbGem = gem;
        }
        private void setPartie()
        {
            int numEnnemis =Serveur.findEnnemis(posClient,posPartie);
            if (numEnnemis != -1)
                Ennemis = Serveur.getEnnemis(numEnnemis);
            else
                Ennemis = null;
            Thread.Sleep(1000);
        }
        private bool estPresent(string[] data)
        {
            Serveur.mutPartie1.WaitOne();
            if (acces.estPresent(data[0], data[1]) && !estConnecter(data[0]))
            {
                sendClient(Moi.sckJoueur, "oui");
                Serveur.mutPartie1.ReleaseMutex();
                return true;
            }
            else
            {
                sendClient(Moi.sckJoueur, "non");
                Serveur.mutPartie1.ReleaseMutex();
                return false;
            }        
        }
         private bool estConnecter(string alias)
        {
            for (int i = 0; i < Serveur.tabJoueur.Count; ++i)
            {
                if (Serveur.tabJoueur[i] != null && Serveur.tabJoueur[i].nom == alias)
                    return true;
            }
            return false;
        }
         private bool peutEtreAjouter(string[] data)
        {
            Serveur.mutPartie1.WaitOne();
            if (acces.estDejaPresent(data[0]))
            {
                sendClient(Moi.sckJoueur, "oui");
                Serveur.mutPartie1.ReleaseMutex();
                return false;
            }
            else
            {
                sendClient(Moi.sckJoueur, "non");
                acces.ajouter(data[0], data[1], data[2], data[3]);
                Serveur.mutPartie1.ReleaseMutex();
                return true;

            }        
        }
        private void sendDeckJoueurClient()
        {
            envoyerDeck(Moi.sckJoueur, monDeck);
        }
        private  string recevoirResultat(Socket client)
        {
            string strData = "";
            try
            {
                byte[] buff = new byte[client.SendBufferSize];
                int bytesRead = client.Receive(buff);
                byte[] formatted = new byte[bytesRead];
                for (int i = 0; i < bytesRead; i++)
                {
                    formatted[i] = buff[i];
                }
                strData = Encoding.ASCII.GetString(formatted);
            }
            //ne fait rien car le client n'a rien envoyer parcequ'il c'est déconnecter
            catch (SocketException)
            {
                Serveur.mutex.WaitOne();
                Console.Write("Erreur de telechargement des donnees");
                Serveur.tabJoueur.Remove(Moi);
                Deconnection = true;
                Serveur.mutex.ReleaseMutex();
            }
            return strData;
        }
        private  void sendClient(Socket client, String text)
        {
            try
            {
                byte[] data = Encoding.ASCII.GetBytes(text);
                client.Send(data);
            }
            catch 
            {
                Serveur.mutex.WaitOne();
                Console.Write("Erreur de telechargement des donnees");
                Serveur.tabJoueur.Remove(Moi);
                Deconnection = true;
                Serveur.mutex.ReleaseMutex();
            }
        }
        private  void sendProfil(string alias)
        {
            Serveur.mutPartie1.WaitOne();
            string profile=acces.getProfil(alias);
            if (profile != null)
            {
                sendClient(Moi.sckJoueur, profile);
            }
            else
            {
                sendClient(Moi.sckJoueur, "non");
            }
            Serveur.mutPartie1.ReleaseMutex();
        }
        private  bool aPerdu(Joueur player)
        { 
            if(player != null && player.vie<=0)
            {
                return true;
            }
            return false;
        }
        private  void sendDeck(string alias)
        {
            string deck = acces.getDeckJoueur(alias);
            if (deck != "")
                sendClient(Moi.sckJoueur, deck);
            else
                sendClient(Moi.sckJoueur, "aucun deck");
        }

    }
}
