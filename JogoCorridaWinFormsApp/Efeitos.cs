using System.IO;
namespace JogoCorridaWinFormsApp
{
    internal class Particula
    {
        public float X, Y, VelX, VelY;
        public float Vida, VidaTotal;
        public float Tamanho;
        public Color Cor;
        public bool Fumaca;
    }

    internal class TextoFlutuante
    {
        public float X, Y;
        public float Vida, VidaTotal;
        public string Texto = "";
        public Color Cor;
    }

    internal class Efeitos
    {
        private readonly List<Particula> particulas = [];
        private readonly List<TextoFlutuante> textos = [];
        private readonly Random rnd = new();

        private static readonly Color[] paletaFogo =
        [
            Color.FromArgb(255, 250, 220),
            Color.FromArgb(255, 214, 74),
            Color.FromArgb(255, 148, 38),
            Color.FromArgb(232, 76, 43),
            Color.FromArgb(180, 32, 32)
        ];

        public void Limpar()
        {
            particulas.Clear();
            textos.Clear();
        }

        public void Explosao(float x, float y, float escala)
        {
            for (int i = 0; i < 55; i++)
            {
                var angulo = rnd.NextDouble() * Math.PI * 2;
                var forca = (60 + rnd.NextDouble() * 300) * escala;
                var duracao = (float)(320 + rnd.NextDouble() * 620);

                particulas.Add(new Particula
                {
                    X = x,
                    Y = y,
                    VelX = (float)(Math.Cos(angulo) * forca),
                    VelY = (float)(Math.Sin(angulo) * forca),
                    Vida = duracao,
                    VidaTotal = duracao,
                    Tamanho = (float)((4 + rnd.NextDouble() * 9) * escala),
                    Cor = paletaFogo[rnd.Next(paletaFogo.Length)]
                });
            }

            for (int i = 0; i < 16; i++)
            {
                var angulo = rnd.NextDouble() * Math.PI * 2;
                var forca = (20 + rnd.NextDouble() * 90) * escala;
                var cinza = 60 + rnd.Next(70);
                var duracao = (float)(700 + rnd.NextDouble() * 700);

                particulas.Add(new Particula
                {
                    X = x,
                    Y = y,
                    VelX = (float)(Math.Cos(angulo) * forca),
                    VelY = (float)(Math.Sin(angulo) * forca) - 40,
                    Vida = duracao,
                    VidaTotal = duracao,
                    Tamanho = (float)((14 + rnd.NextDouble() * 20) * escala),
                    Cor = Color.FromArgb(cinza, cinza, cinza),
                    Fumaca = true
                });
            }
        }

        public void Texto(float x, float y, string texto, Color cor, float duracaoMs = 900)
        {
            textos.Add(new TextoFlutuante
            {
                X = x,
                Y = y,
                Texto = texto,
                Cor = cor,
                Vida = duracaoMs,
                VidaTotal = duracaoMs
            });
        }

        public void Atualizar(double deltaMs)
        {
            var dt = (float)(deltaMs / 1000.0);

            for (int i = particulas.Count - 1; i >= 0; i--)
            {
                var p = particulas[i];
                p.Vida -= (float)deltaMs;
                if (p.Vida <= 0) { particulas.RemoveAt(i); continue; }

                p.X += p.VelX * dt;
                p.Y += p.VelY * dt;

                if (p.Fumaca)
                {
                    p.VelY -= 30 * dt;          
                    p.Tamanho += 22 * dt;
                }
                else
                {
                    p.VelY += 260 * dt; 
                }

                var atrito = 1 - 1.8f * dt;
                p.VelX *= atrito;
                p.VelY *= atrito;
            }

            for (int i = textos.Count - 1; i >= 0; i--)
            {
                var t = textos[i];
                t.Vida -= (float)deltaMs;
                if (t.Vida <= 0) { textos.RemoveAt(i); continue; }
                t.Y -= 55 * dt;
            }
        }

        public void Desenhar(Graphics g, Font fonte)
        {
            foreach (var p in particulas)
            {
                var proporcao = Math.Clamp(p.Vida / p.VidaTotal, 0f, 1f);
                var alfa = (int)((p.Fumaca ? 110 : 255) * proporcao);
                var tamanho = p.Tamanho * (p.Fumaca ? 1 : proporcao);

                using var pincel = new SolidBrush(Color.FromArgb(alfa, p.Cor));
                g.FillEllipse(pincel, p.X - tamanho / 2, p.Y - tamanho / 2, tamanho, tamanho);
            }

            using var formato = new StringFormat
            {
                Alignment = StringAlignment.Center,
                LineAlignment = StringAlignment.Center
            };

            foreach (var t in textos)
            {
                var proporcao = Math.Clamp(t.Vida / t.VidaTotal, 0f, 1f);
                var alfa = (int)(255 * proporcao);

                using var sombra = new SolidBrush(Color.FromArgb(alfa / 2, Color.Black));
                using var pincel = new SolidBrush(Color.FromArgb(alfa, t.Cor));
                g.DrawString(t.Texto, fonte, sombra, t.X + 1, t.Y + 1, formato);
                g.DrawString(t.Texto, fonte, pincel, t.X, t.Y, formato);
            }
        }
    }
}
