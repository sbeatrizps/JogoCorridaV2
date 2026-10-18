using JogoCorrida;

namespace JogoCorridaWinFormsApp
{
    /// <summary>
    /// Ensina o jogo jogando: na PRIMEIRA vez que cada tipo de coisa aparece na pista,
    /// um rotulo salta em cima dela dizendo o que fazer ("DESVIE", "PEGUE").
    ///
    /// E a resposta para o problema de nao dar para saber o que e obstaculo e o que e
    /// bonus: em vez de um texto comprido no inicio, o jogo avisa na hora exata.
    /// </summary>
    internal class Tutorial
    {
        private class Rotulo
        {
            public Elemento Alvo = null!;
            public string Texto = "";
            public Color Cor;
            public double Vida;
        }

        private readonly HashSet<string> jaEnsinados = [];
        private readonly List<Rotulo> rotulos = [];

        /// <summary>Tempo que a dica das setas fica na tela no comeco da partida.</summary>
        private double tempoDaDicaInicial;

        public void Reiniciar()
        {
            jaEnsinados.Clear();
            rotulos.Clear();
            tempoDaDicaInicial = 5000;
        }

        /// <summary>Procura elementos de um tipo que o jogador ainda nao viu.</summary>
        public void Observar(Jogo jogo)
        {
            if (jogo.Estado != EstadoJogo.Jogando) return;

            foreach (var el in jogo.Elementos)
            {
                if (!el.Ativo || el.PosicaoY + el.Altura < 0) continue;

                var (chave, texto, cor) = Classifica(el);
                if (chave == "" || jaEnsinados.Contains(chave)) continue;

                jaEnsinados.Add(chave);
                rotulos.Add(new Rotulo { Alvo = el, Texto = texto, Cor = cor, Vida = 3600 });
            }
        }

        private static (string chave, string texto, Color cor) Classifica(Elemento el) => el.Tipo switch
        {
            TipoElemento.Lagrima => ("lagrima", "PEGUE +50", Tema.Ciano),
            TipoElemento.PowerUp => ("poder:" + el.PowerUp, RotuloDoPoder(el.PowerUp), Tema.Dourado),
            TipoElemento.Obstaculo => el.Obstaculo == TipoObstaculo.Miragem
                ? ("miragem", "INVERTE!", Tema.Ciano)
                : ("obstaculo", "DESVIE!", Tema.Rosa),
            _ => ("", "", Color.White)
        };

        private static string RotuloDoPoder(TipoPowerUp poder) => poder switch
        {
            TipoPowerUp.Empatia => "ESCUDO",
            TipoPowerUp.Proposito => "PONTOS x2",
            TipoPowerUp.Conexao => "IMA",
            _ => "MAIS LENTO"
        };

        public void Atualizar(double deltaMs, bool jogando)
        {
            // a dica das setas so conta tempo enquanto a corrida esta acontecendo
            // (senao ela expira durante a conversa de abertura)
            if (jogando && tempoDaDicaInicial > 0) tempoDaDicaInicial -= deltaMs;

            for (int i = rotulos.Count - 1; i >= 0; i--)
            {
                rotulos[i].Vida -= deltaMs;
                var saiu = rotulos[i].Alvo.PosicaoY > 10000 || !rotulos[i].Alvo.Ativo;
                if (rotulos[i].Vida <= 0 || saiu) rotulos.RemoveAt(i);
            }
        }

        public void Desenhar(Graphics g, Jogo jogo, Font fonte)
        {
            foreach (var rotulo in rotulos)
            {
                var alvo = rotulo.Alvo;
                if (alvo.PosicaoY + alvo.Altura < 0 || alvo.PosicaoY > jogo.Config.Altura) continue;

                var x = (float)alvo.CentroX;
                var y = (float)alvo.PosicaoY - 26;
                var alfa = (int)(255 * Math.Clamp(rotulo.Vida / 600.0, 0, 1));

                // setinha ligando o rotulo ao elemento
                using (var caneta = new Pen(Tema.ComAlfa(rotulo.Cor, alfa), 2))
                {
                    g.DrawLine(caneta, x, y + 16, x, (float)alvo.PosicaoY - 2);
                }

                Tema.Texto(g, rotulo.Texto, fonte, Tema.ComAlfa(rotulo.Cor, alfa),
                           x, y, StringAlignment.Center);
            }
        }

        /// <summary>Dica das setas, no comeco da partida, logo acima de Selene.</summary>
        public void DesenhaDicaInicial(Graphics g, Jogo jogo, Font fonte)
        {
            if (tempoDaDicaInicial <= 0 || jogo.Estado != EstadoJogo.Jogando) return;

            var alfa = (int)(255 * Math.Clamp(tempoDaDicaInicial / 800.0, 0, 1));
            var x = jogo.Config.Largura / 2f;
            var y = (float)jogo.Jogador.PosicaoY - 54;

            Tema.Texto(g, "< SETAS >  TROCAR DE FAIXA", fonte, Tema.ComAlfa(Tema.Branco, alfa),
                       x, y, StringAlignment.Center);
        }
    }
}
