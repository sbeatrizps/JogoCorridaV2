namespace JogoCorrida
{
    /// <summary>
    /// Estado atual da partida. A interface usa o estado para saber o que desenhar.
    /// </summary>
    public enum EstadoJogo
    {
        Menu,
        /// <summary>Conversa entre os personagens, antes de um setor comecar.</summary>
        Dialogo,
        Jogando,
        Pausado,
        /// <summary>Momento da batida: o mundo congela enquanto roda o efeito visual.</summary>
        Colidindo,
        /// <summary>Aviso rapido de troca de setor (nos ciclos sem dialogo).</summary>
        TrocandoSetor,
        GameOver
    }
}
