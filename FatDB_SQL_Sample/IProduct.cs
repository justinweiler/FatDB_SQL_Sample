using FatCloud.Client.FatDB;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FatDB_SQL_Sample
{
    public interface IProduct
    {
        int ProductKey { get; set; }

        string ProductName { get; set; }
    
        decimal Cost { get; set; }

        string Color { get; set; }
    
        string ModelName { get; set; }
 
        string Description { get; set; }   

        FatDBClientVersionInfo GetVersionInfo();
    }
}
