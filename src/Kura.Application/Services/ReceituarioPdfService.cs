namespace Kura.Application.Services;

using Kura.Application.DTOs.Documento;
using Kura.Application.Services.Interfaces;
using Kura.Domain.Entities;
using Kura.Domain.Exceptions;
using Kura.Domain.Interfaces;
using Microsoft.Extensions.Configuration;
using QuestPDF.Fluent;
using QuestPDF.Helpers;

public sealed class ReceituarioPdfService : IReceituarioPdfService
{
    private readonly IEventoClinicoRepository _eventoRepository;
    private readonly IRepository<Prescricao> _prescricaoRepository;
    private readonly IPetRepository _petRepository;
    private readonly IVeterinarioRepository _veterinarioRepository;
    private readonly IRepository<Medicamento> _medicamentoRepository;
    private readonly IRepository<Documento> _documentoRepository;
    private readonly IUnitOfWork _uow;
    private readonly string _storageBasePath;

    public ReceituarioPdfService(
        IEventoClinicoRepository eventoRepository,
        IRepository<Prescricao> prescricaoRepository,
        IPetRepository petRepository,
        IVeterinarioRepository veterinarioRepository,
        IRepository<Medicamento> medicamentoRepository,
        IRepository<Documento> documentoRepository,
        IUnitOfWork uow,
        IConfiguration configuration)
    {
        _eventoRepository = eventoRepository;
        _prescricaoRepository = prescricaoRepository;
        _petRepository = petRepository;
        _veterinarioRepository = veterinarioRepository;
        _medicamentoRepository = medicamentoRepository;
        _documentoRepository = documentoRepository;
        _uow = uow;
        _storageBasePath = configuration["Storage:BasePath"]
            ?? Path.Combine(AppContext.BaseDirectory, "storage", "documentos");
    }

    public async Task<DocumentoResponseDto> GerarReceituarioAsync(long idEventoClinico)
    {
        var evento = await _eventoRepository.GetByIdAsync(idEventoClinico)
            ?? throw new EntidadeNaoEncontradaException("EventoClinico", idEventoClinico);

        var prescricoes = await _prescricaoRepository.FindAsync(p => p.IdEventoClinico == idEventoClinico);
        var prescricao = prescricoes.FirstOrDefault()
            ?? throw new EntidadeNaoEncontradaException("Prescricao", idEventoClinico);

        var pet = await _petRepository.GetByIdAsync(evento.IdPet)
            ?? throw new EntidadeNaoEncontradaException("Pet", evento.IdPet);

        var veterinario = await _veterinarioRepository.GetByIdAsync(evento.IdVeterinario)
            ?? throw new EntidadeNaoEncontradaException("Veterinario", evento.IdVeterinario);

        var medicamento = await _medicamentoRepository.GetByIdAsync(prescricao.IdMedicamento)
            ?? throw new EntidadeNaoEncontradaException("Medicamento", prescricao.IdMedicamento);

        var pdfBytes = MontarPdf(evento, prescricao, pet, veterinario, medicamento);

        Directory.CreateDirectory(_storageBasePath);
        var nomeArquivo = $"receituario-{idEventoClinico}-{Guid.NewGuid():N}.pdf";
        var caminhoCompleto = Path.Combine(_storageBasePath, nomeArquivo);
        await File.WriteAllBytesAsync(caminhoCompleto, pdfBytes);

        var documento = new Documento
        {
            IdEventoClinico = idEventoClinico,
            NmArquivo = nomeArquivo,
            DsTipoMime = "application/pdf",
            DsCaminho = caminhoCompleto,
            NrTamanhoBytes = pdfBytes.LongLength,
        };

        await _documentoRepository.AddAsync(documento);
        await _uow.CommitAsync();

        return new DocumentoResponseDto
        {
            Id = documento.Id,
            IdEventoClinico = documento.IdEventoClinico,
            NmArquivo = documento.NmArquivo,
            DsTipoMime = documento.DsTipoMime,
            DsCaminho = documento.DsCaminho,
            NrTamanhoBytes = documento.NrTamanhoBytes,
        };
    }

    public async Task<ArquivoBinarioDto> ObterArquivoReceituarioAsync(long idEventoClinico, long idDocumento)
    {
        // EventoClinico já está em KuraDbContext.ApplyTenantFilters — se o evento não
        // existir OU pertencer a outra clínica, GetByIdAsync devolve null aqui. As duas
        // situações resultam na mesma exceção/404, sem diferenciar "não existe" de "é de
        // outra clínica" (evita vazar existência via resposta diferenciada — TASK-51).
        _ = await _eventoRepository.GetByIdAsync(idEventoClinico)
            ?? throw new EntidadeNaoEncontradaException("EventoClinico", idEventoClinico);

        var documento = await _documentoRepository.GetByIdAsync(idDocumento);

        // Documento não declara IdClinica própria (é filho de EventoClinico via FK, fora
        // do ApplyTenantFilters por desenho — ver CLAUDE.md). O isolamento de tenant vem
        // exclusivamente daqui: só aceitamos o documento se ele pertencer ao evento que
        // acabou de passar pelo filtro de tenant acima.
        if (documento is null || documento.IdEventoClinico != idEventoClinico)
            throw new EntidadeNaoEncontradaException("Documento", idDocumento);

        // Nunca aceitamos caminho vindo do cliente — DsCaminho vem só do banco. Ainda
        // assim, defesa em profundidade contra path traversal: se o valor persistido
        // (por bug ou adulteração) resolver para fora de Storage:BasePath, recusamos a
        // servir o arquivo em vez de confiar cegamente no que está no banco.
        var caminhoResolvido = Path.GetFullPath(documento.DsCaminho);
        var baseResolvida = Path.GetFullPath(_storageBasePath) + Path.DirectorySeparatorChar;

        if (!caminhoResolvido.StartsWith(baseResolvida, StringComparison.Ordinal))
            throw new EntidadeNaoEncontradaException("Documento", idDocumento);

        if (!File.Exists(caminhoResolvido))
            throw new EntidadeNaoEncontradaException("Documento", idDocumento);

        var bytes = await File.ReadAllBytesAsync(caminhoResolvido);

        return new ArquivoBinarioDto
        {
            Conteudo = bytes,
            NomeArquivo = documento.NmArquivo,
            DsTipoMime = documento.DsTipoMime,
        };
    }

    private static byte[] MontarPdf(
        EventoClinico evento, Prescricao prescricao, Pet pet, Veterinario veterinario, Medicamento medicamento)
    {
        return Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Margin(36);
                page.Size(PageSizes.A4);
                page.DefaultTextStyle(x => x.FontSize(11));

                page.Header().Column(col =>
                {
                    col.Item().Text("Receituário Veterinário").FontSize(18).Bold();
                    col.Item().Text($"{veterinario.NmVeterinario} — CRMV {veterinario.NrCrmv}");
                });

                page.Content().PaddingVertical(16).Column(col =>
                {
                    col.Spacing(8);
                    col.Item().Text($"Paciente: {pet.NmPet}");
                    col.Item().Text($"Data: {evento.DtEvento:dd/MM/yyyy}");

                    col.Item().PaddingTop(12).Text("Medicamento").Bold();
                    col.Item().Text(medicamento.NmMedicamento);
                    col.Item().Text($"Princípio ativo: {medicamento.DsPrincipioAtivo}");
                    col.Item().Text($"Apresentação: {medicamento.DsApresentacao}");
                    col.Item().Text($"Posologia: {prescricao.DsPosologia}");
                    col.Item().Text($"Duração: {prescricao.NrDuracaoDias} dias");
                });

                page.Footer().AlignCenter().Text(
                    "KURA — documento gerado eletronicamente, sem assinatura ICP-Brasil.")
                    .FontSize(8);
            });
        }).GeneratePdf();
    }
}
