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
