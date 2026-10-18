namespace JogoCorrida
{
    /// <summary>
    /// Qualquer coisa que aparece na pista: Selene, um obstaculo, uma lagrima,
    /// um Insight ou o chefe.
    /// </summary>
    public class Elemento
    {
        // ATRIBUTOS DA CLASSE
        public TipoElemento Tipo { get; set; }
        public TipoObstaculo Obstaculo { get; set; } = TipoObstaculo.Nenhum;
        public TipoPowerUp PowerUp { get; set; }

        public double PosicaoX { get; set; }
        public double PosicaoY { get; set; }
        public int Faixa { get; set; }
        public int Altura { get; set; }
        public int Largura { get; set; }

        /// <summary>
        /// Quantas vezes mais rapido que a pista o elemento se aproxima.
        /// Sempre >= 1, para o motor conseguir prever a hora exata da chegada
        /// e garantir que a onda tenha passagem (ver Jogo.GeraOnda).
        /// </summary>
        public double FatorVelocidade { get; set; } = 1.0;

        /// <summary>Pontos ganhos ao superar o obstaculo (ou ao pegar o item).</summary>
        public int Pontos { get; set; }

        /// <summary>Indica que o elemento ja foi contado na pontuacao.</summary>
        public bool Contabilizado { get; set; }
        public bool Ativo { get; set; } = true;

        /// <summary>Contador livre usado pela interface para animacoes.</summary>
        public double Animacao { get; set; }

        /// <summary>Sorteio fixo (0..1) para a interface variar o desenho sem piscar.</summary>
        public double Variacao { get; set; }

        public double CentroX => PosicaoX + Largura / 2.0;
        public double CentroY => PosicaoY + Altura / 2.0;

        public void Movimentar(double x, double y)
        {
            PosicaoX = x;
            PosicaoY = y;
        }

        /// <summary>Testa a sobreposicao das caixas dos dois elementos, com uma folga.</summary>
        public bool Colide(Elemento outro, double folga = 0)
        {
            return PosicaoX + folga < outro.PosicaoX + outro.Largura - folga
                && PosicaoX + Largura - folga > outro.PosicaoX + folga
                && PosicaoY + folga < outro.PosicaoY + outro.Altura - folga
                && PosicaoY + Altura - folga > outro.PosicaoY + folga;
        }

        /// <summary>
        /// Monta um obstaculo ja com as caracteristicas do seu tipo.
        /// As medidas sao proporcionais ao jogador para funcionar em qualquer escala.
        /// </summary>
        public static Elemento CriarObstaculo(TipoObstaculo tipo, Configuracao cfg)
        {
            var ob = new Elemento
            {
                Tipo = TipoElemento.Obstaculo,
                Obstaculo = tipo
            };

            switch (tipo)
            {
                case TipoObstaculo.Vulto:
                    ob.Largura = Escala(cfg.LarguraJogador, 1.0);
                    ob.Altura = Escala(cfg.AlturaJogador, 1.0);
                    ob.FatorVelocidade = 1.0;
                    ob.Pontos = 10;
                    break;

                case TipoObstaculo.Executor:
                    ob.Largura = Escala(cfg.LarguraJogador, 1.35);
                    ob.Altura = Escala(cfg.AlturaJogador, 1.40);
                    ob.FatorVelocidade = 1.0;
                    ob.Pontos = 20;
                    break;

                case TipoObstaculo.Barreira:
                    ob.Largura = Escala(cfg.LarguraJogador, 1.15);
                    ob.Altura = Escala(cfg.AlturaJogador, 0.60);
                    ob.FatorVelocidade = 1.0;
                    ob.Pontos = 8;
                    break;

                case TipoObstaculo.Miragem:
                    ob.Largura = Escala(cfg.LarguraJogador, 1.15);
                    ob.Altura = Escala(cfg.AlturaJogador, 1.15);
                    ob.FatorVelocidade = 1.0;
                    ob.Pontos = 12;
                    break;

                case TipoObstaculo.Drone:
                    ob.Largura = Escala(cfg.LarguraJogador, 1.0);
                    ob.Altura = Escala(cfg.AlturaJogador, 0.75);
                    ob.FatorVelocidade = 1.45;   // chega antes: o motor compensa no spawn
                    ob.Pontos = 25;
                    break;
            }
            return ob;
        }

        public static Elemento CriarLagrima(Configuracao cfg) => new()
        {
            Tipo = TipoElemento.Lagrima,
            Largura = cfg.LarguraLagrima,
            Altura = cfg.AlturaLagrima,
            FatorVelocidade = 1.0,
            Pontos = cfg.PontosPorLagrima
        };

        public static Elemento CriarPowerUp(TipoPowerUp tipo, Configuracao cfg) => new()
        {
            Tipo = TipoElemento.PowerUp,
            PowerUp = tipo,
            Largura = cfg.LarguraPowerUp,
            Altura = cfg.AlturaPowerUp,
            FatorVelocidade = 1.0,
            Pontos = cfg.PontosPorPowerUp
        };

        private static int Escala(int medida, double fator) => Math.Max(1, (int)Math.Round(medida * fator));
    }
}
