using System;
using System.Windows;
using System.Windows.Controls;

namespace CMinus.Tests.Wpf
{
    public partial class LogView : UserControl
    {
        public LogView()
        {
            InitializeComponent();
        }

        protected override void OnInitialized(EventArgs e)
        {
            base.OnInitialized(e);

            var app = Application.Current as App;

            if (app is not null)
            {
                app.Log = Log;
            }
        }

        public void Log(string message)
        {
            this.TextBox.Text += $"{message}\n";
        }
    }
}
