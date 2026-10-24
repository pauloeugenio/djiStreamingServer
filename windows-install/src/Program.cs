using System;
using System.Reflection;
using System.Threading;
using System.Windows.Forms;

[assembly: AssemblyTitle("Drone Streaming Server")]
[assembly: AssemblyDescription("Servidor local de transmissão de drones")]
[assembly: AssemblyCompany("Paulo Eugenio")]
[assembly: AssemblyProduct("Drone Streaming Server")]
[assembly: AssemblyVersion("1.0.0.0")]
[assembly: AssemblyFileVersion("1.0.0.0")]
namespace DroneStreamingServer
{
    internal static class Program
    {
        [STAThread]
        static void Main()
        {
            bool created;
            using (var mutex = new Mutex(true, @"Local\DroneStreamingServer.Desktop", out created))
            {
                if (!created) { MessageBox.Show("O Drone Streaming Server já está aberto. Verifique a barra de tarefas.", "Drone Streaming Server"); return; }
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
                Application.ThreadException += (sender, e) => MessageBox.Show(e.Exception.Message, "Erro no aplicativo", MessageBoxButtons.OK, MessageBoxIcon.Error);
                try { Application.Run(new MainWindow()); }
                catch (Exception ex) { MessageBox.Show("Não foi possível abrir o aplicativo.\n\n" + ex.Message, "Drone Streaming Server", MessageBoxButtons.OK, MessageBoxIcon.Error); }
            }
        }
    }
}
