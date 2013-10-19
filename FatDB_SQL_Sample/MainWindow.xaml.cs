using FatCloud.Client.FatDB;
using FatCloud.Client.FatDBMS;
using FatCloud.Client.FatDBMS.Model;
using FatCloud.Client.QueryProvider;
using FatCloud.FatDB.Contracts;
using FatDB_SQL_Sample.FatDB_SQL_SampleDataSetTableAdapters;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace FatDB_SQL_Sample
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private List<object> _originalProducts;

        public MainWindow()
        {
            InitializeComponent();
        }

        private void sqlGrid_Loaded(object sender, RoutedEventArgs e)
        {            
            sqlGridRefresh_Click(null, null);
        }

        private void fatdbGrid_Loaded(object sender, RoutedEventArgs e)
        {
            fatdbGridRefresh_Click(null, null);
        }

        private void fatdbGridRefresh_Click(object sender, RoutedEventArgs e)
        {
            if (fatdb_db.IsChecked == true)
            {
                using (FatDBConnection conxn = new FatDBConnection())
                {
                    int totalRecords;

                    var client = conxn.CreateClient<product>();
                    var clientResponse = client.SelectRecordsByIndex(new List<FatDBIndexQuery>() { new FatDBIndexQuery("TOC") }, 0, 1000, out totalRecords);
                    var sortedList = new List<product>(clientResponse.RecordData.ConvertAll<product>(x => { return new product(x); }));                    
                    sortedList.Sort((product x, product y) => { return x.ProductKey.CompareTo(y.ProductKey); });
                    fatdbGrid.ItemsSource = sortedList;                    
                    _originalProducts = new List<object>(sortedList.ConvertAll<product>(x => { return new product(x, true); }));                    
                }
            }
            else
            {
                using (FatDBMSConnection conxn = new FatDBMSConnection())
                {
                    var client = conxn.CreateClient();
                    List<Record> recordList = client.ReadRange("product_cache", typeof(product_cache).ToString(), null, 1000);
                    var sortedList = new List<product_cache>(recordList.ConvertAll<product_cache>(x => { return new product_cache(x); }));
                    sortedList.Sort((product_cache x, product_cache y) => { return x.ProductKey.CompareTo(y.ProductKey); });                    
                    fatdbGrid.ItemsSource = sortedList;                    
                    _originalProducts = new List<object>(sortedList.ConvertAll<product_cache>(x => { return new product_cache(x); }));
                }
            }
        }
        
        private void fatdbGridUpdate_Click(object sender, RoutedEventArgs e)
        {            
            fatdbGrid.CommitEdit(DataGridEditingUnit.Row, true);

            if (fatdb_db.IsChecked == true)
            {
                _update<product>();
            }
            else
            {
                _update<product_cache>();
            }
        }

        private void _update<T>() where T : IProduct
        {
            var updateList = new List<T>();

            foreach (T currentProduct in fatdbGrid.Items.SourceCollection)
            {
                var originalIndex = _originalProducts.FindIndex(x => { return ((T)x).ProductKey == currentProduct.ProductKey; });

                // new item
                if (originalIndex == -1)
                {
                    updateList.Add(currentProduct);
                    continue;
                }
                
                var originalProduct = (T)_originalProducts[originalIndex];

                // updated item
                if (originalProduct.ProductName != currentProduct.ProductName ||
                    originalProduct.ModelName   != currentProduct.ModelName ||
                    originalProduct.Color       != currentProduct.Color ||
                    originalProduct.Cost        != currentProduct.Cost ||
                    originalProduct.Description != currentProduct.Description)
                {
                    updateList.Add(currentProduct);
                }

                _originalProducts.RemoveAt(originalIndex);            
            }

            if (updateList.Count > 0 || _originalProducts.Count > 0)
            { 
                using (FatDBConnection conxn = new FatDBConnection())
                {
                    var client = conxn.CreateClient<T>();

                    if (updateList.Count > 0)
                    {
                        foreach (T newProduct in updateList)
                        {
                            var vi = newProduct.GetVersionInfo();

                            if (vi == null)
                            {
                                var response = client.SelectRecord(new FatDBRecordKey(newProduct.ProductKey.ToString()));

                                if (response.HasData == true && response.HasVersionInfo == true)
                                {
                                    vi = response.RecordData[0].VersionInfo;
                                }
                                else
                                {
                                    vi = new FatDBClientVersionInfo(-1, DateTime.UtcNow);
                                }
                            }

                            client.InsertRecord(newProduct, new FatDBClientVersionInfo(vi.Version + 1, DateTime.UtcNow));
                        }
                    }

                    if (_originalProducts.Count > 0)
                    {
                        foreach (T deletedProduct in _originalProducts)
                        {
                            client.DeleteRecord(new FatDBRecordKey(deletedProduct.ProductKey.ToString()));
                        }
                    }
                }

                fatdbGridRefresh_Click(null, null);
            }            
        }       

        private void sqlGridRefresh_Click(object sender, RoutedEventArgs e)
        {
            sqlGrid.ItemsSource = ((ProductTableAdapter)sqlGrid.DataContext).GetData();
        }

        private void sqlGridUpdate_Click(object sender, RoutedEventArgs e)
        {
            ((ProductTableAdapter)sqlGrid.DataContext).Update((FatDB_SQL_SampleDataSet.ProductDataTable)sqlGrid.ItemsSource);
            sqlGridRefresh_Click(null, null);
        }

        private void sqlGridQuery_Click(object sender, RoutedEventArgs e)
        {
            using (var db = new Linq2SQLDataContext())
            { 
                var results = from product in db.Products where product.ProductKey > 500 
                                  && product.ProductKey < 511 select product;
                
                var productList = results.ToList<Product>();
                var sortedList = productList.ConvertAll<product>(x => { return new product(x); });
                sortedList.Sort((product x, product y) => { return x.ProductKey.CompareTo(y.ProductKey); });
                
                var dataTable = new FatDB_SQL_SampleDataSet.ProductDataTable();
                
                foreach (var p in sortedList)
                {
                    dataTable.LoadDataRow(new object[]{ p.ProductKey, p.ProductName, p.Cost, p.Color, p.ModelName, p.Description }, LoadOption.OverwriteChanges);
                }
                
                sqlGrid.ItemsSource = dataTable;
            }
        }

        private void fatdbGridQuery_Click(object sender, RoutedEventArgs e)
        {
            using (var conxn = new FatDBConnection())
            {
                var db = new FatDBQueryableFactory(conxn);
                var results = from product in db.Queryable<product>() where product.ProductKey > 500 
                                  && product.ProductKey < 511 select product;
                
                var sortedList = results.ToList<product>();
                sortedList.Sort((product x, product y) => { return x.ProductKey.CompareTo(y.ProductKey); });
                fatdbGrid.ItemsSource = sortedList;
                _originalProducts = new List<object>(sortedList.ConvertAll<product>(x => { return new product(x, false); }));                    
            }
        }

        private void fatdbGridCache_Click(object sender, RoutedEventArgs e)
        {
            using (FatDBConnection conxn = new FatDBConnection())
            {
                var client = conxn.CreateClient<product_cache>();                    

                for (int i = 501; i <= 510; i++)
                {
                    var response = client.SelectRecord(new FatDBRecordKey(i.ToString()));
                }
            }

            fatdbGridRefresh_Click(null, null);
        }

        private static SolidColorBrush _blue = new BrushConverter().ConvertFromString("#FF294FCC") as SolidColorBrush;
        private static SolidColorBrush _gray = new BrushConverter().ConvertFromString("Gray") as SolidColorBrush;

        private void fatdb_cache_Click(object sender, RoutedEventArgs e)
        {
            fatdbGridQuery.IsEnabled  = false;
            fatdbGridQuery.Foreground = _gray;
            fatdbGridCache.IsEnabled  = true;
            fatdbGridCache.Foreground = _blue;
            fatdbGridRefresh_Click(null, null);
        }

        private void fatdb_db_Click(object sender, RoutedEventArgs e)
        {
            fatdbGridQuery.IsEnabled  = true;
            fatdbGridQuery.Foreground = _blue;
            fatdbGridCache.IsEnabled  = false;
            fatdbGridCache.Foreground = _gray;
            fatdbGridRefresh_Click(null, null);
        }
    }
}
