using SaasVistoria.Domain;

namespace SaasVistoria.Infrastructure;

/// <summary>
/// Catálogo de modelos padrão de vistoria imobiliária (pt-BR).
/// Estruturado segundo a prática de laudos de entrada/saída: medidores e chaves,
/// instalações gerais, acabamentos por ambiente e áreas externas.
/// </summary>
public static class TemplateCatalog
{
    // ---- Conjuntos de tópicos reutilizáveis (acabamentos padrão de um ambiente) ----
    private static readonly string[] Acabamento = ["Piso", "Paredes e pintura", "Teto / forro", "Rodapés", "Porta e fechadura", "Janelas e vidros"];
    private static readonly string[] Eletrica = ["Tomadas", "Interruptores", "Luminárias e lâmpadas"];
    private static readonly string[] Dormitorio = [.. Acabamento, .. Eletrica, "Armário embutido", "Persiana / cortineiro"];
    private static readonly string[] Social = [.. Acabamento, .. Eletrica];

    private static readonly string[] Cozinha = [
        "Piso", "Paredes e revestimento", "Teto / forro", "Porta e fechadura", "Janela e vidros",
        "Bancada e cuba", "Torneira", "Sifão e ralo", "Armários e gabinete",
        "Ponto de gás", "Exaustor / coifa", "Tomadas e interruptores", "Iluminação"];

    private static readonly string[] Banheiro = [
        "Piso e rejunte", "Revestimento das paredes", "Teto / forro", "Porta e fechadura",
        "Vaso sanitário", "Pia, cuba e gabinete", "Torneira e metais", "Chuveiro / ducha",
        "Box e vedação", "Espelho", "Acessórios (papeleira, toalheiro)", "Ralo e sifão",
        "Ventilação / janela", "Tomadas e iluminação"];

    private static readonly string[] AreaServico = [
        "Piso e ralo", "Paredes e revestimento", "Tanque", "Torneira",
        "Ponto de máquina de lavar", "Varal", "Armários", "Ventilação", "Tomadas e iluminação"];

    private static readonly string[] Varanda = [
        "Piso", "Paredes e pintura", "Guarda-corpo / vidros", "Cobertura",
        "Ponto de água", "Tomadas e iluminação", "Esquadrias"];

    private static readonly string[] Garagem = [
        "Piso e demarcação", "Paredes e pintura", "Portão e automatizador",
        "Controle remoto entregue", "Iluminação", "Tomadas", "Infiltrações"];

    // Bloco obrigatório em qualquer vistoria de entrada/saída
    private static readonly string[] MedidoresChaves = [
        "Leitura do hidrômetro (água)", "Leitura do relógio de luz (energia)", "Leitura do medidor de gás",
        "Chaves da porta principal (quantidade)", "Chaves de serviço / secundárias",
        "Controle do portão", "Tag / cartão de acesso", "Chave da caixa de correio"];

    private static readonly string[] InstalacoesGerais = [
        "Quadro de disjuntores", "Fiação aparente / emendas", "Caixa d'água e boia",
        "Registros e válvulas", "Infiltrações e mofo", "Fissuras estruturais",
        "Campainha / interfone", "Detector de fumaça / extintor"];

    private static readonly string[] Fachada = [
        "Muro e gradil", "Portão de entrada", "Pintura externa", "Calçada",
        "Telhado e calhas", "Iluminação externa", "Numeração do imóvel"];

    private static readonly string[] Quintal = [
        "Piso externo", "Jardim e vegetação", "Muros e divisas", "Torneira externa",
        "Churrasqueira", "Iluminação externa", "Portão dos fundos"];

    private static readonly string[] Mobiliario = [
        "Sofá e poltronas", "Mesa e cadeiras", "Camas e colchões", "Guarda-roupas",
        "Geladeira", "Fogão / cooktop", "Micro-ondas", "Máquina de lavar",
        "Ar-condicionado", "Televisor", "Cortinas e persianas", "Utensílios e enxoval"];

    private static TemplateRoom R(string name, params string[] topics) => new(name, topics);

    /// <summary>Modelos do sistema, prontos para uso e clonáveis pela imobiliária.</summary>
    public static IReadOnlyList<(string Name, string Description, PropertyType? Type, TemplateRoom[] Rooms)> All =>
    [
        ("Studio / Kitnet", "Ambiente único integrado — ideal para locação compacta.", PropertyType.Apartamento, [
            R("Medidores e chaves", MedidoresChaves),
            R("Ambiente integrado", [.. Social, "Bancada / cozinha compacta", "Cuba e torneira", "Armários"]),
            R("Banheiro", Banheiro),
            R("Instalações gerais", InstalacoesGerais)]),

        ("Apartamento 1 quarto", "Padrão para apartamentos de um dormitório.", PropertyType.Apartamento, [
            R("Medidores e chaves", MedidoresChaves),
            R("Sala de estar", Social),
            R("Cozinha", Cozinha),
            R("Área de serviço", AreaServico),
            R("Quarto", Dormitorio),
            R("Banheiro social", Banheiro),
            R("Varanda / sacada", Varanda),
            R("Instalações gerais", InstalacoesGerais)]),

        ("Apartamento 2 quartos / 1 banheiro", "O modelo mais usado em locação residencial.", PropertyType.Apartamento, [
            R("Medidores e chaves", MedidoresChaves),
            R("Sala de estar / jantar", Social),
            R("Cozinha", Cozinha),
            R("Área de serviço", AreaServico),
            R("Quarto 1", Dormitorio),
            R("Quarto 2", Dormitorio),
            R("Banheiro social", Banheiro),
            R("Varanda / sacada", Varanda),
            R("Garagem / vaga", Garagem),
            R("Instalações gerais", InstalacoesGerais)]),

        ("Apartamento 2 quartos / 2 banheiros", "Dois dormitórios com suíte e banheiro social.", PropertyType.Apartamento, [
            R("Medidores e chaves", MedidoresChaves),
            R("Hall de entrada", [.. Acabamento, .. Eletrica]),
            R("Sala de estar / jantar", Social),
            R("Cozinha", Cozinha),
            R("Área de serviço", AreaServico),
            R("Suíte", Dormitorio),
            R("Banheiro da suíte", Banheiro),
            R("Quarto 2", Dormitorio),
            R("Banheiro social", Banheiro),
            R("Varanda / sacada", Varanda),
            R("Garagem / vaga", Garagem),
            R("Instalações gerais", InstalacoesGerais)]),

        ("Apartamento 3 quartos com suíte", "Alto padrão residencial com dependências.", PropertyType.Apartamento, [
            R("Medidores e chaves", MedidoresChaves),
            R("Hall de entrada", [.. Acabamento, .. Eletrica]),
            R("Sala de estar", Social),
            R("Sala de jantar", Social),
            R("Cozinha", Cozinha),
            R("Área de serviço", AreaServico),
            R("Lavabo", Banheiro),
            R("Suíte máster", Dormitorio),
            R("Banheiro da suíte", Banheiro),
            R("Quarto 2", Dormitorio),
            R("Quarto 3", Dormitorio),
            R("Banheiro social", Banheiro),
            R("Corredor", [.. Acabamento, .. Eletrica]),
            R("Varanda gourmet", [.. Varanda, "Churrasqueira", "Bancada"]),
            R("Garagem / vaga", Garagem),
            R("Instalações gerais", InstalacoesGerais)]),

        ("Casa 2 quartos", "Casa térrea com área externa.", PropertyType.Casa, [
            R("Medidores e chaves", MedidoresChaves),
            R("Fachada e entrada", Fachada),
            R("Sala de estar / jantar", Social),
            R("Cozinha", Cozinha),
            R("Área de serviço", AreaServico),
            R("Quarto 1", Dormitorio),
            R("Quarto 2", Dormitorio),
            R("Banheiro social", Banheiro),
            R("Quintal / área externa", Quintal),
            R("Garagem", Garagem),
            R("Instalações gerais", InstalacoesGerais)]),

        ("Casa 3 quartos com suíte", "Casa familiar completa com áreas externas.", PropertyType.Casa, [
            R("Medidores e chaves", MedidoresChaves),
            R("Fachada e entrada", Fachada),
            R("Sala de estar", Social),
            R("Sala de jantar", Social),
            R("Cozinha", Cozinha),
            R("Área de serviço", AreaServico),
            R("Lavabo", Banheiro),
            R("Suíte", Dormitorio),
            R("Banheiro da suíte", Banheiro),
            R("Quarto 2", Dormitorio),
            R("Quarto 3", Dormitorio),
            R("Banheiro social", Banheiro),
            R("Corredor", [.. Acabamento, .. Eletrica]),
            R("Quintal / área externa", Quintal),
            R("Churrasqueira / área gourmet", ["Piso", "Bancada e cuba", "Churrasqueira e grelha", "Coifa", "Iluminação", "Tomadas"]),
            R("Garagem", Garagem),
            R("Instalações gerais", InstalacoesGerais)]),

        ("Casa com piscina", "Imóvel de lazer — inclui piscina e equipamentos.", PropertyType.Casa, [
            R("Medidores e chaves", MedidoresChaves),
            R("Fachada e entrada", Fachada),
            R("Sala de estar / jantar", Social),
            R("Cozinha", Cozinha),
            R("Área de serviço", AreaServico),
            R("Suíte", Dormitorio),
            R("Banheiro da suíte", Banheiro),
            R("Quarto 2", Dormitorio),
            R("Quarto 3", Dormitorio),
            R("Banheiro social", Banheiro),
            R("Piscina", ["Revestimento e azulejos", "Borda e deck", "Bomba e filtro", "Aquecimento", "Iluminação subaquática", "Cerca de proteção", "Limpeza e nível da água"]),
            R("Área gourmet", ["Piso", "Bancada e cuba", "Churrasqueira", "Coifa", "Iluminação", "Tomadas"]),
            R("Quintal / jardim", Quintal),
            R("Garagem", Garagem),
            R("Instalações gerais", InstalacoesGerais)]),

        ("Imóvel mobiliado — inventário", "Complemento para locação mobiliada: bens e eletrodomésticos.", null, [
            R("Medidores e chaves", MedidoresChaves),
            R("Inventário de mobiliário", Mobiliario),
            R("Sala de estar", Social),
            R("Cozinha", Cozinha),
            R("Quarto", Dormitorio),
            R("Banheiro", Banheiro),
            R("Instalações gerais", InstalacoesGerais)]),

        ("Loja / ponto comercial", "Vitrine, atendimento e instalações comerciais.", PropertyType.Comercial, [
            R("Medidores e chaves", MedidoresChaves),
            R("Fachada e vitrine", ["Letreiro / testeira", "Vidros da vitrine", "Porta de enrolar", "Toldo", "Iluminação da fachada", "Pintura externa"]),
            R("Salão de atendimento", [.. Social, "Ar-condicionado", "Forro modular"]),
            R("Estoque / depósito", ["Piso", "Paredes", "Prateleiras", "Ventilação", "Iluminação"]),
            R("Copa", ["Bancada e cuba", "Torneira", "Armários", "Piso", "Tomadas"]),
            R("Banheiro", Banheiro),
            R("Instalações e segurança", [.. InstalacoesGerais, "Extintor e sinalização", "Saída de emergência", "Sistema de alarme", "Pontos de rede / lógica"])]),

        ("Sala comercial / escritório", "Conjunto corporativo em edifício comercial.", PropertyType.Comercial, [
            R("Medidores e chaves", MedidoresChaves),
            R("Recepção", [.. Social, "Ar-condicionado"]),
            R("Área de trabalho", [.. Social, "Forro modular", "Ar-condicionado", "Pontos de rede / lógica", "Piso elevado"]),
            R("Sala de reunião", [.. Social, "Divisórias", "Ar-condicionado"]),
            R("Copa", ["Bancada e cuba", "Torneira", "Armários", "Piso", "Tomadas"]),
            R("Banheiro", Banheiro),
            R("Instalações e segurança", [.. InstalacoesGerais, "Extintor e sinalização", "Saída de emergência", "Ar-condicionado central"])]),

        ("Galpão / depósito", "Área industrial ou logística.", PropertyType.Comercial, [
            R("Medidores e chaves", MedidoresChaves),
            R("Área externa e acesso", ["Portão de acesso", "Pátio de manobra", "Docas", "Muros e cercas", "Iluminação externa"]),
            R("Área interna", ["Piso industrial", "Paredes e alvenaria", "Estrutura metálica", "Telhado e telhas", "Calhas e rufos", "Ventilação e exaustores", "Iluminação"]),
            R("Escritório de apoio", [.. Social, "Ar-condicionado"]),
            R("Banheiro / vestiário", Banheiro),
            R("Instalações e segurança", [.. InstalacoesGerais, "Sistema de combate a incêndio", "Hidrantes", "Para-raios", "Cabine primária de energia"])]),

        ("Terreno / lote", "Vistoria de terreno sem edificação.", PropertyType.Terreno, [
            R("Identificação e limites", ["Demarcação e divisas", "Muros e cercas", "Portão de acesso", "Numeração / matrícula"]),
            R("Condições do solo", ["Nivelamento e topografia", "Erosão / voçorocas", "Entulho e resíduos", "Vegetação e mato", "Drenagem e escoamento"]),
            R("Infraestrutura disponível", ["Ponto de água", "Ponto de energia", "Rede de esgoto", "Guia e sarjeta", "Calçada", "Iluminação pública"])]),

        ("Vistoria de manutenção periódica", "Acompanhamento durante a locação — foco em conservação.", null, [
            R("Estrutura e vedação", ["Fissuras e trincas", "Infiltrações", "Mofo e umidade", "Telhado e calhas", "Impermeabilização"]),
            R("Instalações elétricas", ["Quadro de disjuntores", "Tomadas e interruptores", "Fiação aparente", "Iluminação", "Aterramento"]),
            R("Instalações hidráulicas", ["Vazamentos aparentes", "Pressão da água", "Registros e válvulas", "Caixa d'água", "Ralos e sifões", "Esgoto e odores"]),
            R("Esquadrias e acabamentos", ["Portas e fechaduras", "Janelas e vidros", "Pintura geral", "Pisos e rodapés", "Revestimentos"]),
            R("Conservação geral", ["Limpeza do imóvel", "Jardim e áreas externas", "Uso conforme contrato", "Benfeitorias não autorizadas"])])
    ];
}
