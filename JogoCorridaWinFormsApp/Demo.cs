using JogoCorrida;

namespace JogoCorridaWinFormsApp
{
    /// <summary>
    /// Modo demonstracao: o jogo se joga sozinho (tecla F2, ou o argumento --demo).
    /// Util para deixar rodando numa apresentacao.
    ///
    /// A logica e a mesma usada nos testes do motor: so entra numa faixa se ela, e
    /// todo o caminho ate ela, estiver livre durante a travessia - contando o corpo
    /// inteiro do jogador, nao so a cabeca.
    /// </summary>
    internal static class Demo
    {
        private static double esperaMs;

        public static bool Ativo { get; set; }

        public static void Alternar() => Ativo = !Ativo;

        public static void Jogar(Jogo jogo, double deltaMs)
        {
            if (!Ativo) return;

            // nas telas de inicio e de fim, espera um pouco e recomeca sozinho
            if (jogo.Estado is EstadoJogo.Menu or EstadoJogo.GameOver or EstadoJogo.Dialogo)
            {
                esperaMs += deltaMs;
                if (esperaMs > (jogo.Estado == EstadoJogo.Dialogo ? 2600 : 4000))
                {
                    esperaMs = 0;
                    jogo.Confirmar();
                }
                return;
            }

            esperaMs = 0;
            if (jogo.Estado != EstadoJogo.Jogando) return;

            jogo.MoverParaFaixa(FaixaSegura(jogo));
        }

        /// <summary>Escolhe a melhor faixa: a que fica livre por mais tempo e da para alcancar.</summary>
        private static int FaixaSegura(Jogo jogo)
        {
            var cfg = jogo.Config;
            var topo = jogo.Jogador.PosicaoY;
            var baseJogador = topo + jogo.Jogador.Altura;
            var velocidade = Math.Max(1, jogo.VelocidadeAtual);

            // em quantos segundos a faixa fica ocupada (0 = ja esta; infinito = livre)
            double LivrePor(int faixa)
            {
                var menor = double.PositiveInfinity;
                foreach (var ob in jogo.Obstaculos)
                {
                    if (!ob.Ativo || ob.Faixa != faixa) continue;
                    if (ob.Obstaculo == TipoObstaculo.Miragem) continue;  // nao machuca

                    var velocidadeOb = velocidade * ob.FatorVelocidade;
                    var comeca = (topo - (ob.PosicaoY + ob.Altura)) / velocidadeOb;
                    var termina = (baseJogador - ob.PosicaoY) / velocidadeOb;
                    if (termina <= 0) continue;                 // ja passou pelo corpo inteiro

                    menor = Math.Min(menor, Math.Max(0, comeca));
                }
                return menor;
            }

            var livre = Enumerable.Range(0, cfg.QtdFaixas).Select(LivrePor).ToArray();
            var bonus = Enumerable.Range(0, cfg.QtdFaixas).Select(f => Atrativo(jogo, f)).ToArray();

            var melhor = jogo.FaixaAlvo;
            var melhorNota = double.NegativeInfinity;

            for (int destino = 0; destino < cfg.QtdFaixas; destino++)
            {
                var passos = Math.Abs(destino - jogo.FaixaAlvo);
                var viagem = passos * cfg.TempoPorFaixa;

                var de = Math.Min(destino, jogo.FaixaAlvo);
                var ate = Math.Max(destino, jogo.FaixaAlvo);
                var caminhoLivre = true;
                for (int k = de; k <= ate; k++)
                {
                    if (livre[k] <= viagem + 0.10) { caminhoLivre = false; break; }
                }
                if (!caminhoLivre) continue;

                var nota = Math.Min(livre[destino], 10) + bonus[destino] - passos * 0.05;
                if (nota > melhorNota) { melhorNota = nota; melhor = destino; }
            }

            return melhor;
        }

        /// <summary>Da uma puxadinha para as faixas que tem coracao ou power-up chegando.</summary>
        private static double Atrativo(Jogo jogo, int faixa)
        {
            var nota = 0.0;
            foreach (var item in jogo.Elementos)
            {
                if (!item.Ativo || item.Faixa != faixa) continue;
                if (item.Tipo is not (TipoElemento.Lagrima or TipoElemento.PowerUp)) continue;
                if (item.PosicaoY > jogo.Jogador.PosicaoY) continue;

                nota += item.Tipo == TipoElemento.PowerUp ? 1.2 : 0.5;
            }
            return nota;
        }
    }
}
