# CartolaOptimizationSolution
1. Visão Geral da Solução
A solução foi arquitetada sob o paradigma de Clean Architecture (Arquitetura Limpa), visando o desacoplamento de código, testabilidade e aplicação rigorosa dos princípios SOLID e Clean Code. A stack tecnológica baseia-se em .NET 8 e C#.
Estrutura de Projetos (Solution):
CartolaOptimization.Api (Web API): Camada de apresentação e interface REST. Gerencia a injeção de dependência e os endpoints.
CartolaOptimization.Application (Class Library): Orquestração da lógica de aplicação, DTOs, mapeamentos e interfaces de serviço.
CartolaOptimization.Domain (Class Library): O "Coração" do sistema. Contém as entidades de negócio puras (Atletas, Clubes, Escalações) e regras de domínio, sem dependências externas.
CartolaOptimization.Infrastructure (Class Library): Implementação de acesso a dados (Entity Framework Core), conexão com MySQL e integrações com APIs externas (Scraper do Cartola).
CartolaOptimization.ML (Class Library): Módulo especializado em Inteligência Artificial, contendo os modelos do ML.NET e o algoritmo de otimização combinatória.
2. Configurações de Controle de Versão (DevOps)
Repositório: Inicializado via Git e hospedado no GitHub.
Ignorance de Arquivos: Configuração de um arquivo .gitignore robusto para o ecossistema .NET, garantindo que arquivos binários (/bin, /obj), configurações locais do Visual Studio (.vs) e segredos de ambiente não sejam versionados.
3. Gestão de Dependências (Pacotes NuGet)
Para suportar as funcionalidades planejadas, foram instalados e configurados os seguintes pacotes:
Acesso a Dados: Pomelo.EntityFrameworkCore.MySql (Provider para MySQL) e Microsoft.EntityFrameworkCore.Design.
Inteligência Artificial: Microsoft.ML e Microsoft.ML.FastTree (para algoritmos de regressão e predição de pontuação).
Integração: Microsoft.Extensions.ML na API para otimização do consumo do modelo via PredictionEnginePool.
4. Persistência e Mapeamento
A gestão do banco de dados segue o padrão Fluent API em vez de Data Annotations, isolando as preocupações de infraestrutura da camada de domínio:
AppDbContext: Configurado no projeto Infrastructure para gerenciar transações e persistência.
Mapeamento Separado: Criação de classes IEntityTypeConfiguration para cada entidade, garantindo que o esquema do MySQL seja definido de forma granular e limpa.
Injeção de Dependência: O ciclo de vida do contexto foi definido como Scoped no Program.cs, respeitando o padrão de uma instância por requisição HTTP.
5. Padrões de Código Adotados
Nomenclatura: Uso de nomes semânticos em inglês para compatibilidade com padrões da indústria e bibliotecas .NET.
Interfaces: Aplicação do Princípio de Inversão de Dependência (D do SOLID), onde a aplicação depende de abstrações e não de implementações concretas.
Async/Await: Implementação de chamadas assíncronas de ponta a ponta para garantir a escalabilidade da API.