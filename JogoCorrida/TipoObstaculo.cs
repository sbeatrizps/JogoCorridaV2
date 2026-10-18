namespace JogoCorrida
{
    /// <summary>
    /// Os obstaculos do jogo. Sao poucos e sempre com o mesmo comportamento,
    /// para o jogador aprender rapido - o que muda de um setor para o outro e
    /// so a aparencia (um agente da Nex Gen, um neosage do culto, um anjo podre).
    ///
    /// Todos se aproximam a uma velocidade IGUAL OU MAIOR que a da pista
    /// (FatorVelocidade >= 1), para o motor conseguir prever a hora exata da
    /// chegada e garantir que sempre exista passagem (ver Jogo.GeraOnda).
    /// </summary>
    public enum TipoObstaculo
    {
        Nenhum,
        /// <summary>Figura de pe na faixa. O inimigo comum.</summary>
        Vulto,
        /// <summary>Grandalhao: ocupa mais espaco e vale mais pontos.</summary>
        Executor,
        /// <summary>Bloqueio baixo no chao.</summary>
        Barreira,
        /// <summary>Nao machuca: inverte os controles por alguns segundos.</summary>
        Miragem,
        /// <summary>Vem mais rapido que o resto da pista.</summary>
        Drone
    }
}
