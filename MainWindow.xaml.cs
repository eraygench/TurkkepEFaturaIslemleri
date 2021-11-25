using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.ServiceModel;
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
using TurkkepService;

namespace TurkkepFaturaIslemleri
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();

            BasicHttpBinding binding = new BasicHttpBinding();
            binding.Name = "IntegrationServiceSoap";
            binding.CloseTimeout = TimeSpan.Parse("00:01:00");
            binding.OpenTimeout = TimeSpan.Parse("00:01:00");
            binding.ReceiveTimeout = TimeSpan.Parse("00:01:00");
            binding.SendTimeout = TimeSpan.Parse("00:01:00");
            binding.AllowCookies = false;
            binding.BypassProxyOnLocal = false;
            binding.MaxBufferSize = int.MaxValue; // 65536;
            binding.MaxBufferPoolSize = int.MaxValue;
            binding.MaxReceivedMessageSize = int.MaxValue; // 65536;
            binding.TextEncoding = Encoding.UTF8;
            binding.TransferMode = TransferMode.Buffered;
            binding.UseDefaultWebProxy = true;
            binding.ReaderQuotas.MaxArrayLength = int.MaxValue; // 16384;
            binding.ReaderQuotas.MaxBytesPerRead = 4096;
            binding.ReaderQuotas.MaxNameTableCharCount = int.MaxValue; // 16384;
            binding.Security.Mode = BasicHttpSecurityMode.Transport;
            binding.Security.Transport.ClientCredentialType = HttpClientCredentialType.Windows;
            binding.Security.Transport.ProxyCredentialType = HttpProxyCredentialType.None;
            binding.Security.Message.ClientCredentialType = BasicHttpMessageCredentialType.Certificate;

            System.Net.ServicePointManager.ServerCertificateValidationCallback +=
            (se, cert, chain, sslerror) =>
            {
                return true;
            };

            client = new EFaturaEntegrasyon2SoapClient(binding, new EndpointAddress("https://efintws.turkkep.com.tr/EFaturaEntegrasyon2.asmx?wsdl"));

            bakiyeSorguWorker.DoWork += BakiyeSorguWorker_DoWork;
            bakiyeSorguWorker.RunWorkerCompleted += BakiyeSorguWorker_RunWorkerCompleted;

            txtUsername.Text = txtUsername.ToolTip.ToString();
            txtPassword.Text = txtPassword.ToolTip.ToString();

            txtPassword.GotFocus += RemoveText;
            txtPassword.LostFocus += AddText;
            txtUsername.GotFocus += RemoveText;
            txtUsername.LostFocus += AddText;
        }

        private void BakiyeSorguWorker_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            lblBakiyeBilgi.Content =
                "E-Fatura Bakiye: " + efaturaCredit +
                "\nE-Arşiv Bakiye: " + earsivCredit +
                "\nE-İrsaliye Bakiye: " + eirsaliyeCredit;
            btnBakiyeSorgula.Visibility = Visibility.Hidden;
        }

        private void BakiyeSorguWorker_DoWork(object sender, DoWorkEventArgs e)
        {
            if (!string.IsNullOrEmpty(token))
            {
                efaturaCredit = client.EFaturaKalanKontorSorgula(new EFaturaKalanKontorSorgulaRequest
                {
                    token = token
                }).EFaturaKalanKontorSorgulaResult;
                earsivCredit = client.EArsivKalanKontorSorgula(new EArsivKalanKontorSorgulaRequest
                {
                    token = token
                }).EArsivKalanKontorSorgulaResult;
                eirsaliyeCredit = client.EIrsaliyeKalanKontorSorgula(new EIrsaliyeKalanKontorSorgulaRequest
                {
                    token = token
                }).EIrsaliyeKalanKontorSorgulaResult;
            }
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(token))
            {
                token = client.OturumAc(new OturumAcRequest
                {
                    kullaniciAdi = txtUsername.Text,
                    kullaniciSifresi = txtPassword.Text
                }).OturumAcResult;

                if(string.IsNullOrEmpty(token))
                {
                    MessageBox.Show("Kullanıcı adı veya şifre yanlış", "Uyarı", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                btnLogin.IsEnabled = false;
                this.Title = this.Title + " - Giriş Yapıldı";
                btnBakiyeSorgula.Visibility = Visibility.Visible;
                btnFaturalariGetir.Visibility = Visibility.Visible;
            }
        }

        private string token;
        public long efaturaCredit = 0;
        public long earsivCredit = 0;
        public long eirsaliyeCredit = 0;
        private EFaturaEntegrasyon2SoapClient client;

        BackgroundWorker bakiyeSorguWorker = new BackgroundWorker();

        private void btnBakiyeSorgula_Click(object sender, RoutedEventArgs e)
        {
            if (!bakiyeSorguWorker.IsBusy)
            {
                btnBakiyeSorgula.IsEnabled = false;
                bakiyeSorguWorker.RunWorkerAsync();
            }
        }

        private void btnFaturalariGetir_Click(object sender, RoutedEventArgs e)
        {
            if (!string.IsNullOrEmpty(token))
            {
                var invoices = client.YeniGelenFaturalariListele(new YeniGelenFaturalariListeleRequest
                {
                    token = token
                });
                dgFaturalar.ItemsSource = invoices.YeniGelenFaturalariListeleResult;
            }
        }

        private void dgFaturalar_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            var invoice = dgFaturalar.SelectedItem as Invoice;
            if (invoice != null && !string.IsNullOrEmpty(token))
            {
                var bytes = client.GelenFaturaPdfAl(new GelenFaturaPdfAlRequest
                {
                    token = token, 
                    faturaNo = invoice.InvoiceId
                });
                if (bytes.GelenFaturaPdfAlResult != null && bytes.GelenFaturaPdfAlResult.Length > 0)
                {
                    File.WriteAllBytes(@"C:\TEMP\" + invoice.InvoiceReference + ".pdf", bytes.GelenFaturaPdfAlResult);
                    Process.Start(@"C:\TEMP\" + invoice.InvoiceReference + ".pdf");
                }
                else
                {
                    MessageBox.Show("Fatura bulunamadı");
                }
            }
        }

        public void RemoveText(object sender, EventArgs e)
        {
            if ((sender as TextBox).Text == (sender as TextBox).ToolTip.ToString())
            {
                (sender as TextBox).Text = "";
            }
        }

        public void AddText(object sender, EventArgs e)
        {
            if (string.IsNullOrWhiteSpace((sender as TextBox).Text))
                (sender as TextBox).Text = (sender as TextBox).ToolTip.ToString();
        }
    }
}
