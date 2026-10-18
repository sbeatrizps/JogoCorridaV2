using System.Runtime.InteropServices;
using JogoCorrida;
using System.IO;

namespace JogoCorridaWinFormsApp
{
    /// <summary>
    /// Efeitos sonoros do jogo. Os sons sao gerados por programacao (WAV em memoria),
    /// entao os efeitos nao dependem de nenhum arquivo de audio externo.
    /// (A trilha de fundo, essa sim, vem de arquivos MP3 - ver <see cref="Musica"/>.)
    ///
    /// A reproducao usa a funcao PlaySound do Windows com o audio ja copiado para
    /// memoria nao gerenciada. Isso e necessario porque o som toca de forma assincrona:
    /// se o buffer fosse um vetor comum do .NET, o coletor de lixo poderia move-lo
    /// enquanto o Windows ainda estivesse lendo.
    /// </summary>
    internal static class Sons
    {
        private enum Onda { Senoide, Quadrada, Serra, Ruido }

        /// <summary>Um pedaco do efeito: frequencia inicial/final, duracao e volume.</summary>
        private readonly record struct Nota(double Freq, double FreqFinal, double DuracaoMs, double Volume, Onda Forma);

        [DllImport("winmm.dll", EntryPoint = "PlaySoundW", CharSet = CharSet.Unicode)]
        private static extern bool PlaySound(IntPtr dados, IntPtr modulo, uint opcoes);

        private const uint SND_ASYNC = 0x0001;
        private const uint SND_NODEFAULT = 0x0002;
        private const uint SND_MEMORY = 0x0004;
        private const uint SND_PURGE = 0x0040;

        private const int TaxaAmostragem = 22050;

        // os buffers ficam alocados ate o jogo fechar - o Windows le direto deles
        private static readonly Dictionary<EventoSom, IntPtr> buffers = [];
        private static readonly Dictionary<EventoSom, double> duracoes = [];
        private static readonly Random rnd = new();

        private static DateTime fimDoSomAtual = DateTime.MinValue;
        private static int prioridadeAtual;

        public static bool Ligado { get; set; } = true;

        public static void Inicializar()
        {
            if (buffers.Count > 0) return;

            Registrar(EventoSom.Inicio,
            [
                new Nota(196, 196, 70, 0.22, Onda.Serra),
                new Nota(294, 392, 120, 0.22, Onda.Quadrada),
                new Nota(587, 784, 160, 0.20, Onda.Quadrada)
            ]);

            Registrar(EventoSom.TrocaFaixa,
            [
                new Nota(480, 660, 40, 0.09, Onda.Quadrada)
            ]);

            Registrar(EventoSom.Lagrima,
            [
                new Nota(988, 988, 50, 0.20, Onda.Quadrada),
                new Nota(1319, 1319, 100, 0.20, Onda.Quadrada)
            ]);

            Registrar(EventoSom.ObstaculoSuperado,
            [
                new Nota(680, 860, 38, 0.08, Onda.Senoide)
            ]);

            Registrar(EventoSom.PegouPowerUp,
            [
                new Nota(523, 523, 60, 0.20, Onda.Quadrada),
                new Nota(784, 784, 60, 0.20, Onda.Quadrada),
                new Nota(1047, 1319, 180, 0.20, Onda.Quadrada)
            ]);

            Registrar(EventoSom.EscudoQuebrado,
            [
                new Nota(1, 1, 120, 0.26, Onda.Ruido),
                new Nota(880, 220, 220, 0.22, Onda.Quadrada)
            ]);

            Registrar(EventoSom.Miragem,
            [
                new Nota(1, 1, 160, 0.18, Onda.Ruido),
                new Nota(300, 900, 90, 0.14, Onda.Quadrada),
                new Nota(900, 300, 90, 0.14, Onda.Quadrada)
            ]);

            Registrar(EventoSom.Colisao,
            [
                new Nota(1, 1, 260, 0.40, Onda.Ruido),
                new Nota(180, 48, 280, 0.34, Onda.Serra)
            ]);

            Registrar(EventoSom.ChefeEntrou,
            [
                new Nota(1, 1, 180, 0.26, Onda.Ruido),
                new Nota(140, 55, 520, 0.34, Onda.Serra),
                new Nota(70, 70, 260, 0.30, Onda.Quadrada)
            ]);

            Registrar(EventoSom.ChefeDerrotado,
            [
                new Nota(110, 110, 120, 0.28, Onda.Serra),
                new Nota(1, 1, 140, 0.26, Onda.Ruido),
                new Nota(392, 523, 130, 0.24, Onda.Quadrada),
                new Nota(659, 1047, 320, 0.24, Onda.Quadrada)
            ]);

            Registrar(EventoSom.NovoSetor,
            [
                new Nota(523, 523, 100, 0.22, Onda.Quadrada),
                new Nota(659, 659, 100, 0.22, Onda.Quadrada),
                new Nota(784, 784, 100, 0.22, Onda.Quadrada),
                new Nota(1047, 1047, 200, 0.22, Onda.Quadrada)
            ]);

            Registrar(EventoSom.GameOver,
            [
                new Nota(392, 392, 200, 0.26, Onda.Serra),
                new Nota(330, 330, 200, 0.26, Onda.Serra),
                new Nota(262, 262, 200, 0.26, Onda.Serra),
                new Nota(196, 140, 480, 0.28, Onda.Serra)
            ]);

            Registrar(EventoSom.Fala,
            [
                new Nota(620, 760, 26, 0.07, Onda.Quadrada)
            ]);

            Registrar(EventoSom.NovoCiclo,
            [
                new Nota(523, 523, 110, 0.24, Onda.Quadrada),
                new Nota(659, 659, 110, 0.24, Onda.Quadrada),
                new Nota(784, 784, 110, 0.24, Onda.Quadrada),
                new Nota(1047, 1047, 160, 0.24, Onda.Quadrada),
                new Nota(784, 784, 90, 0.24, Onda.Quadrada),
                new Nota(1047, 1319, 460, 0.24, Onda.Quadrada)
            ]);
        }

        /// <summary>
        /// Toca o efeito do evento. Como o Windows so reproduz um efeito por vez,
        /// um som curto (ex.: troca de faixa) nao interrompe um mais importante.
        /// </summary>
        public static void Tocar(EventoSom evento)
        {
            if (!Ligado || !buffers.TryGetValue(evento, out var buffer)) return;

            var prioridade = Prioridade(evento);
            if (DateTime.Now < fimDoSomAtual && prioridade < prioridadeAtual) return;

            try
            {
                PlaySound(buffer, IntPtr.Zero, SND_MEMORY | SND_ASYNC | SND_NODEFAULT);
                prioridadeAtual = prioridade;
                fimDoSomAtual = DateTime.Now.AddMilliseconds(duracoes[evento]);
            }
            catch
            {
                Ligado = false; // sem placa de som: o jogo continua normalmente
            }
        }

        /// <summary>Interrompe qualquer efeito em reproducao (usado ao fechar o jogo).</summary>
        public static void Parar()
        {
            try { PlaySound(IntPtr.Zero, IntPtr.Zero, SND_PURGE); } catch { }
        }

        private static int Prioridade(EventoSom evento) => evento switch
        {
            EventoSom.ChefeEntrou or EventoSom.ChefeDerrotado or EventoSom.GameOver
                or EventoSom.NovoCiclo or EventoSom.NovoSetor or EventoSom.Colisao => 4,
            EventoSom.EscudoQuebrado or EventoSom.PegouPowerUp => 3,
            EventoSom.Lagrima or EventoSom.Miragem or EventoSom.Inicio => 2,
            EventoSom.Fala => 1,
            EventoSom.TrocaFaixa => 1,
            _ => 0
        };

        private static void Registrar(EventoSom evento, Nota[] notas)
        {
            var wav = GerarWav(notas);
            var buffer = Marshal.AllocHGlobal(wav.Length);
            Marshal.Copy(wav, 0, buffer, wav.Length);

            buffers[evento] = buffer;
            duracoes[evento] = notas.Sum(n => n.DuracaoMs);
        }

        /// <summary>Monta um arquivo WAV (PCM 16 bits, mono) com as notas informadas.</summary>
        private static byte[] GerarWav(Nota[] notas)
        {
            var amostras = new List<short>();
            foreach (var nota in notas) GerarNota(nota, amostras);

            var dados = new byte[amostras.Count * 2];
            for (int i = 0; i < amostras.Count; i++)
            {
                dados[i * 2] = (byte)(amostras[i] & 0xFF);
                dados[i * 2 + 1] = (byte)((amostras[i] >> 8) & 0xFF);
            }

            using var memoria = new MemoryStream();
            using var escritor = new BinaryWriter(memoria);

            escritor.Write("RIFF"u8.ToArray());
            escritor.Write(36 + dados.Length);
            escritor.Write("WAVE"u8.ToArray());
            escritor.Write("fmt "u8.ToArray());
            escritor.Write(16);                       // tamanho do bloco fmt
            escritor.Write((short)1);                 // PCM
            escritor.Write((short)1);                 // mono
            escritor.Write(TaxaAmostragem);
            escritor.Write(TaxaAmostragem * 2);       // bytes por segundo
            escritor.Write((short)2);                 // alinhamento
            escritor.Write((short)16);                // bits por amostra
            escritor.Write("data"u8.ToArray());
            escritor.Write(dados.Length);
            escritor.Write(dados);
            escritor.Flush();

            return memoria.ToArray();
        }

        private static void GerarNota(Nota nota, List<short> saida)
        {
            var total = (int)(TaxaAmostragem * nota.DuracaoMs / 1000.0);
            double fase = 0;

            for (int i = 0; i < total; i++)
            {
                var avanco = total <= 1 ? 0 : (double)i / (total - 1);
                var frequencia = nota.Freq + (nota.FreqFinal - nota.Freq) * avanco;
                fase += 2 * Math.PI * frequencia / TaxaAmostragem;

                var valor = nota.Forma switch
                {
                    Onda.Senoide => Math.Sin(fase),
                    Onda.Quadrada => Math.Sin(fase) >= 0 ? 1 : -1,
                    Onda.Serra => 2 * (fase / (2 * Math.PI) % 1) - 1,
                    _ => rnd.NextDouble() * 2 - 1
                };

                saida.Add((short)(valor * nota.Volume * Envelope(avanco) * short.MaxValue));
            }
        }

        /// <summary>Sobe rapido e desce no final, para o som nao estalar.</summary>
        private static double Envelope(double avanco)
        {
            const double ataque = 0.03;
            const double queda = 0.35;

            if (avanco < ataque) return avanco / ataque;
            if (avanco > 1 - queda) return (1 - avanco) / queda;
            return 1;
        }
    }
}
