using LoneEftDmaRadar.UI.Radar.ViewModels;
using System.Globalization;
using System.Windows.Controls;
using System.Windows.Data;

namespace LoneEftDmaRadar.UI.Radar.Views
{
    public partial class MakcuTab : UserControl
    {
        public MakcuViewModel ViewModel { get; }

        public MakcuTab()
        {
            InitializeComponent();
            DataContext = ViewModel = new MakcuViewModel();
        }
    } 
}