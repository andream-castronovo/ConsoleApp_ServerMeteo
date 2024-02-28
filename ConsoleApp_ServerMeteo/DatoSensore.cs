using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Remoting;
using System.Text;
using System.Threading.Tasks;

namespace ConsoleApp_ServerMeteo
{
    /*
     * Studenti: Andrea Maria Castronovo e Francesco Caravita
     * Classe: 5I
     * Gruppo: 3
     * Data: 21/02/2024
     * Descrizione: Codice server per progetto Stazione meteo
    */

    internal class DatoSensore
    {
        public int IDSensore { get; set; }
        public string data { get; set; }
        public string ora { get;set; }
        public object valore { get; set; }
        public string dataConOra()
        {
            return data.Replace("/","-")+"+"+ora.Replace("/","-");
        }
    }
}
 