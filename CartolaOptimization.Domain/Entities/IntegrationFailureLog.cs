namespace CartolaOptimization.Domain.Entities
{
    /// <summary>
    /// Persiste diagnósticos detalhados para as falhas que ocorrem ao coletar ou sincronizar conjuntos de dados externos.
    /// </summary>
    public sealed class IntegrationFailureLog
    {
        /// <summary>
        /// Obtém ou define o identificador primário da linha de log de falha.
        /// </summary>
        public int Id { get; set; }

        /// <summary>
        /// Obtém ou define o timestamp UTC em que a falha foi capturada.
        /// </summary>
        public DateTime OccurredAtUtc { get; set; }

        /// <summary>
        /// Obtém ou define o id de correlação usado para agrupar todos os passos de uma execução de sincronização.
        /// </summary>
        public string CorrelationId { get; set; } = string.Empty;

        /// <summary>
        /// Obtém ou define a fonte externa envolvida na falha, como Cartola ou GatoMestre.
        /// </summary>
        public string SourceSystem { get; set; } = string.Empty;

        /// <summary>
        /// Obtém ou define o nome do processo que estava em execução quando a falha ocorreu.
        /// </summary>
        public string ProcessName { get; set; } = string.Empty;

        /// <summary>
        /// Obtém ou define o passo do pipeline que estava em execução quando a falha ocorreu.
        /// </summary>
        public string StepName { get; set; } = string.Empty;

        /// <summary>
        /// Obtém ou define o método ou componente onde a falha foi detectada.
        /// </summary>
        public string ErrorLocation { get; set; } = string.Empty;

        /// <summary>
        /// Obtém ou define o método HTTP usado na requisição externa.
        /// </summary>
        public string RequestMethod { get; set; } = string.Empty;

        /// <summary>
        /// Obtém ou define a URL absoluta da requisição usada ao chamar a API externa.
        /// </summary>
        public string RequestUrl { get; set; } = string.Empty;

        /// <summary>
        /// Obtém ou define os cabeçalhos de requisição serializados.
        /// </summary>
        public string RequestHeadersJson { get; set; } = string.Empty;

        /// <summary>
        /// Obtém ou define o payload de requisição serializado enviado à API externa.
        /// </summary>
        public string RequestPayload { get; set; } = string.Empty;

        /// <summary>
        /// Obtém ou define o código de status HTTP retornado pela API externa quando disponível.
        /// </summary>
        public int? ResponseStatusCode { get; set; }

        /// <summary>
        /// Obtém ou define a reason phrase HTTP retornada pela API externa quando disponível.
        /// </summary>
        public string ResponseReasonPhrase { get; set; } = string.Empty;

        /// <summary>
        /// Obtém ou define os cabeçalhos de resposta serializados.
        /// </summary>
        public string ResponseHeadersJson { get; set; } = string.Empty;

        /// <summary>
        /// Obtém ou define o payload de resposta bruto recebido da API externa.
        /// </summary>
        public string ResponsePayload { get; set; } = string.Empty;

        /// <summary>
        /// Obtém ou define a lista serializada de passos executados antes da falha.
        /// </summary>
        public string ExecutedStepsJson { get; set; } = string.Empty;

        /// <summary>
        /// Obtém ou define o contexto de negócio serializado associado à falha.
        /// </summary>
        public string ContextJson { get; set; } = string.Empty;

        /// <summary>
        /// Obtém ou define o código de erro de negócio normalizado associado à falha.
        /// </summary>
        public string ErrorCode { get; set; } = string.Empty;

        /// <summary>
        /// Obtém ou define a severidade atribuída à entrada de falha, como Warning ou Error.
        /// </summary>
        public string Severity { get; set; } = string.Empty;

        /// <summary>
        /// Obtém ou define o tipo da exceção levantada pelo processo.
        /// </summary>
        public string ExceptionType { get; set; } = string.Empty;

        /// <summary>
        /// Obtém ou define a mensagem da exceção.
        /// </summary>
        public string ExceptionMessage { get; set; } = string.Empty;

        /// <summary>
        /// Obtém ou define a mensagem da exceção interna quando disponível.
        /// </summary>
        public string InnerExceptionMessage { get; set; } = string.Empty;

        /// <summary>
        /// Obtém ou define o stack trace capturado da exceção.
        /// </summary>
        public string StackTrace { get; set; } = string.Empty;

        /// <summary>
        /// Obtém ou define um valor que indica se a transação principal de sincronização sofreu rollback.
        /// </summary>
        public bool TransactionRolledBack { get; set; }
    }
}
