using Microsoft.Windows.ApplicationModel.DynamicDependency;

namespace ZUI
{
    internal class Program
    {
        [System.STAThread]
        static void Main(string[] args)
        {
            Bootstrap.Initialize(0x00010008);
            Microsoft.UI.Xaml.Application.Start((p) => { var app = new App(); });
        }
    }
}