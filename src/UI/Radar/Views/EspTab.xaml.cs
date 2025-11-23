using LoneEftDmaRadar.UI.Radar.ViewModels;
using System.Windows.Controls;

namespace LoneEftDmaRadar.UI.Radar.Views
{
    public partial class EspTab : UserControl
    {
        public EspViewModel ViewModel { get; }

        public EspTab()
        {
            InitializeComponent();
            DataContext = ViewModel = new EspViewModel();
        }
    }
}