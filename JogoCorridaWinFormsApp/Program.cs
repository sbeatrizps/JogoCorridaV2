using System.IO;
using System.Linq;
namespace JogoCorridaWinFormsApp
{
    internal static class Program
    {
        private static readonly string ArquivoDeErro = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "JogoCorrida", "erro.txt");

        [STAThread]
        static void Main(string[] argumentos)
        {
            Demo.Ativo = argumentos.Any(a => a.Equals("--demo", StringComparison.OrdinalIgnoreCase));

            ApplicationConfiguration.Initialize();

            Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
            Application.ThreadException += (_, e) => RegistraErro(e.Exception);
            AppDomain.CurrentDomain.UnhandledException += (_, e) => RegistraErro(e.ExceptionObject as Exception);

            Application.Run(new FormJogoCorrida());
        }

        private static void RegistraErro(Exception? erro)
        {
            if (erro is null) return;

            try
            {
                var pasta = Path.GetDirectoryName(ArquivoDeErro);
                if (!string.IsNullOrEmpty(pasta)) Directory.CreateDirectory(pasta);
                File.AppendAllText(ArquivoDeErro, $"{DateTime.Now:g}{Environment.NewLine}{erro}{Environment.NewLine}{Environment.NewLine}");
            }
            catch
            {
            }

            MessageBox.Show($"Ocorreu um erro inesperado:{Environment.NewLine}{Environment.NewLine}{erro.Message}",
                            "Jogo Corrida", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }
}
