using System.Drawing.Drawing2D;
using System.IO;

namespace JogoCorridaWinFormsApp
{
    /// <summary>
    /// Paleta e ferramentas de desenho da identidade visual do jogo:
    /// preto quase puro, neon rosa/ciano/verde, brilho e ruido.
    /// Inspirado no clima do album "POST HUMAN: NeX GEn" (ver Assets/CREDITOS.txt).
    /// </summary>
    internal static class Tema
    {
        public static readonly Color Rosa = Color.FromArgb(255, 45, 122);
        public static readonly Color Ciano = Color.FromArgb(34, 227, 255);
        public static readonly Color Verde = Color.FromArgb(182, 255, 60);
        public static readonly Color Dourado = Color.FromArgb(255, 214, 90);
        public static readonly Color Branco = Color.FromArgb(240, 244, 255);
        public static readonly Color Cinza = Color.FromArgb(132, 140, 165);
        public static readonly Color Preto = Color.FromArgb(7, 6, 12);

        public static Color De(uint argb) => Color.FromArgb(unchecked((int)argb));

        public static Color ComAlfa(Color cor, int alfa)
            => Color.FromArgb(Math.Clamp(alfa, 0, 255), cor.R, cor.G, cor.B);

        public static Color Misturar(Color de, Color ate, double fracao)
        {
            fracao = Math.Clamp(fracao, 0, 1);
            return Color.FromArgb(
                (int)(de.A + (ate.A - de.A) * fracao),
                (int)(de.R + (ate.R - de.R) * fracao),
                (int)(de.G + (ate.G - de.G) * fracao),
                (int)(de.B + (ate.B - de.B) * fracao));
        }

        /// <summary>Escurece a cor mantendo o matiz (usado nas silhuetas).</summary>
        public static Color Escurecer(Color cor, double fator)
            => Color.FromArgb(cor.A, (int)(cor.R * fator), (int)(cor.G * fator), (int)(cor.B * fator));

        // =====================================================================
        // DESENHO
        // =====================================================================

        /// <summary>Linha com brilho: varias passadas grossas e transparentes embaixo.</summary>
        public static void LinhaNeon(Graphics g, Color cor, float x1, float y1, float x2, float y2,
                                     float espessura = 3f, float brilho = 1f)
        {
            for (int i = 3; i >= 1; i--)
            {
                using var caneta = new Pen(ComAlfa(cor, (int)(26 * brilho)), espessura * (1 + i * 1.7f))
                {
                    StartCap = LineCap.Round,
                    EndCap = LineCap.Round
                };
                g.DrawLine(caneta, x1, y1, x2, y2);
            }

            using var principal = new Pen(cor, espessura) { StartCap = LineCap.Round, EndCap = LineCap.Round };
            g.DrawLine(principal, x1, y1, x2, y2);
        }

        /// <summary>Retangulo preenchido com brilho em volta.</summary>
        public static void BarraNeon(Graphics g, Color cor, RectangleF r, float brilho = 1f)
        {
            for (int i = 3; i >= 1; i--)
            {
                using var pincel = new SolidBrush(ComAlfa(cor, (int)(22 * brilho)));
                g.FillRectangle(pincel, RectangleF.Inflate(r, i * 2.5f, i * 2.5f));
            }
            using var cheio = new SolidBrush(cor);
            g.FillRectangle(cheio, r);
        }

        /// <summary>Mancha de luz redonda (halo).</summary>
        public static void Halo(Graphics g, Color cor, float centroX, float centroY, float raio, int alfa = 70)
        {
            using var caminho = new GraphicsPath();
            caminho.AddEllipse(centroX - raio, centroY - raio, raio * 2, raio * 2);

            using var pincel = new PathGradientBrush(caminho)
            {
                CenterColor = ComAlfa(cor, alfa),
                SurroundColors = [ComAlfa(cor, 0)]
            };
            g.FillPath(pincel, caminho);
        }

        /// <summary>Texto com aberracao cromatica (copia rosa e ciano deslocadas).</summary>
        public static void TextoGlitch(Graphics g, string texto, Font fonte, Color cor,
                                       float x, float y, StringAlignment alinhamento, float forca = 2f)
        {
            using var formato = new StringFormat { Alignment = alinhamento };

            using (var sombra = new SolidBrush(Color.FromArgb(170, 0, 0, 0)))
            {
                g.DrawString(texto, fonte, sombra, x + 2, y + 2, formato);
            }
            using (var canal = new SolidBrush(ComAlfa(Rosa, 150)))
            {
                g.DrawString(texto, fonte, canal, x - forca, y, formato);
            }
            using (var canal = new SolidBrush(ComAlfa(Ciano, 150)))
            {
                g.DrawString(texto, fonte, canal, x + forca, y, formato);
            }
            using (var principal = new SolidBrush(cor))
            {
                g.DrawString(texto, fonte, principal, x, y, formato);
            }
        }

        /// <summary>Texto simples, so com sombra.</summary>
        public static void Texto(Graphics g, string texto, Font fonte, Color cor,
                                 float x, float y, StringAlignment alinhamento)
        {
            using var formato = new StringFormat { Alignment = alinhamento };
            using var sombra = new SolidBrush(Color.FromArgb(150, 0, 0, 0));
            using var pincel = new SolidBrush(cor);
            g.DrawString(texto, fonte, sombra, x + 1.5f, y + 1.5f, formato);
            g.DrawString(texto, fonte, pincel, x, y, formato);
        }

        /// <summary>Linhas horizontais de monitor antigo por cima de tudo.</summary>
        public static void Scanlines(Graphics g, int largura, int altura, int alfa = 26)
        {
            using var caneta = new Pen(Color.FromArgb(alfa, 0, 0, 0), 1);
            for (int y = 0; y < altura; y += 3)
            {
                g.DrawLine(caneta, 0, y, largura, y);
            }
        }

        /// <summary>Cantos escuros, como lente de camera.</summary>
        public static void Vinheta(Graphics g, int largura, int altura, int alfa = 150)
        {
            var margem = Math.Max(largura, altura) * 0.42f;

            using var caminho = new GraphicsPath();
            caminho.AddEllipse(-margem, -margem, largura + margem * 2, altura + margem * 2);

            using var pincel = new PathGradientBrush(caminho)
            {
                CenterColor = Color.FromArgb(0, 0, 0, 0),
                SurroundColors = [Color.FromArgb(alfa, 0, 0, 0)],
                CenterPoint = new PointF(largura / 2f, altura / 2f)
            };
            g.FillPath(pincel, caminho);
        }

        /// <summary>Chuvisco: pontinhos claros espalhados.</summary>
        public static void Ruido(Graphics g, Random rnd, int largura, int altura, int quantidade, int alfa = 40)
        {
            using var pincel = new SolidBrush(Color.FromArgb(alfa, 255, 255, 255));
            for (int i = 0; i < quantidade; i++)
            {
                var x = rnd.Next(largura);
                var y = rnd.Next(altura);
                g.FillRectangle(pincel, x, y, rnd.Next(1, 3), 1);
            }
        }

        /// <summary>Caminho em forma de coracao dentro do retangulo.</summary>
        public static GraphicsPath Coracao(RectangleF r)
        {
            var caminho = new GraphicsPath();
            var meio = r.Width / 2;

            caminho.AddArc(r.X, r.Y, meio, meio * 0.95f, 180, 180);
            caminho.AddArc(r.X + meio, r.Y, meio, meio * 0.95f, 180, 180);
            caminho.AddLine(r.Right, r.Y + r.Height * 0.42f, r.X + meio, r.Bottom);
            caminho.AddLine(r.X + meio, r.Bottom, r.X, r.Y + r.Height * 0.42f);
            caminho.CloseFigure();

            return caminho;
        }

        /// <summary>Retangulo de cantos arredondados.</summary>
        public static GraphicsPath Arredondado(RectangleF r, float raio)
        {
            raio = Math.Max(1, Math.Min(raio, Math.Min(r.Width, r.Height) / 2));
            var diametro = raio * 2;
            var caminho = new GraphicsPath();

            caminho.AddArc(r.X, r.Y, diametro, diametro, 180, 90);
            caminho.AddArc(r.Right - diametro, r.Y, diametro, diametro, 270, 90);
            caminho.AddArc(r.Right - diametro, r.Bottom - diametro, diametro, diametro, 0, 90);
            caminho.AddArc(r.X, r.Bottom - diametro, diametro, diametro, 90, 90);
            caminho.CloseFigure();

            return caminho;
        }
    }
}
