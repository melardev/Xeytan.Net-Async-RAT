using System.Diagnostics;
using XeytanCSharpServer.Models;

namespace XeytanCSharpServer.Ui.Console.Views
{
    abstract class ClientView : IView
    {
        public Client Client { get; set; }
        private string BannerFormat { get; } = "XeytanCSharp/{0}/{1}>$ ";

        public void SetActiveClient(Client client)
        {
            Client = client;
        }

        public virtual void PrintBanner()
        {
            Debug.Assert(Client != null);
            System.Console.Write(BannerFormat, Client.Id, GetBannerLabel() ?? "");
        }

        protected abstract string GetBannerLabel();

        public abstract string Loop();
    }
}