using LoneEftDmaRadar.UI.Radar.ViewModels;
using System.Windows.Controls;

namespace LoneEftDmaRadar.UI.Radar.Views
{
    public partial class MemWritesTab : UserControl
    {
        public MemWritesViewModel ViewModel { get; }

        public MemWritesTab()
        {
            InitializeComponent();
            DataContext = ViewModel = new MemWritesViewModel();
        }
    }
}