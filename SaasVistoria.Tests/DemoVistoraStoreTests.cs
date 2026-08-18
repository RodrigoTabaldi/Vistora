using SaasVistoria.Application;
using SaasVistoria.Domain;
using SaasVistoria.Infrastructure;

namespace SaasVistoria.Tests;

public class DemoVistoraStoreTests
{
    private static DemoVistoraStore NewStore() => new();

    private static Inspection NewBlankInspection(DemoVistoraStore store) =>
        store.AddInspection(new CreateInspection(store.Properties[0].Id, "Vistoria de entrada", DateTime.Today, "Ana Ribeiro", null));

    [Fact]
    public void AddInspection_UnknownProperty_ThrowsKeyNotFound()
    {
        var store = NewStore();
        Assert.Throws<KeyNotFoundException>(() =>
            store.AddInspection(new CreateInspection(Guid.NewGuid(), "Vistoria de entrada", DateTime.Today, "Ana Ribeiro", null)));
    }

    [Fact]
    public void AddInspection_WithoutTemplate_StartsEmptyAtZeroPercent()
    {
        var store = NewStore();
        var inspection = NewBlankInspection(store);
        Assert.Equal(0, inspection.Completion);
        Assert.Equal(0, inspection.PendingItems);
        Assert.Empty(store.GetItems(inspection.Id));
    }

    [Fact]
    public void AddInspection_WithTemplate_MaterializesOneItemPerTopic()
    {
        var store = NewStore();
        var template = store.Templates.First(t => t.IsSystem);
        var inspection = store.AddInspection(new CreateInspection(store.Properties[0].Id, "Vistoria de entrada", DateTime.Today, "Ana Ribeiro", template.Id));
        var expectedCount = template.Rooms.Sum(r => r.Topics.Count);
        Assert.Equal(expectedCount, store.GetItems(inspection.Id).Count);
    }

    [Fact]
    public void UpdateItem_RecalculatesCompletionAndPendingCount()
    {
        var store = NewStore();
        var inspection = NewBlankInspection(store);
        var item1 = store.AddItem(inspection.Id, new CreateItem("Sala", "Piso"));
        var item2 = store.AddItem(inspection.Id, new CreateItem("Sala", "Paredes"));

        store.UpdateItem(inspection.Id, item1.Id, new UpdateItem(ConditionStatus.Bom, ""));
        var afterFirst = store.FindInspection(inspection.Id)!;
        Assert.Equal(50, afterFirst.Completion);
        Assert.Equal(0, afterFirst.PendingItems);

        store.UpdateItem(inspection.Id, item2.Id, new UpdateItem(ConditionStatus.Danificado, "Vidro trincado"));
        var afterSecond = store.FindInspection(inspection.Id)!;
        Assert.Equal(100, afterSecond.Completion);
        Assert.Equal(1, afterSecond.PendingItems);
    }

    [Fact]
    public void UpdateItem_PromotesStatusFromAgendadaToEmAndamento_OnFirstEvaluation()
    {
        var store = NewStore();
        var inspection = NewBlankInspection(store);
        var item = store.AddItem(inspection.Id, new CreateItem("Sala", "Piso"));
        Assert.Equal(InspectionStatus.Agendada, store.FindInspection(inspection.Id)!.Status);

        store.UpdateItem(inspection.Id, item.Id, new UpdateItem(ConditionStatus.Bom, ""));
        Assert.Equal(InspectionStatus.EmAndamento, store.FindInspection(inspection.Id)!.Status);
    }

    [Fact]
    public void RemoveItem_RecalculatesCompletionAfterDeletion()
    {
        var store = NewStore();
        var inspection = NewBlankInspection(store);
        var item1 = store.AddItem(inspection.Id, new CreateItem("Sala", "Piso"));
        var item2 = store.AddItem(inspection.Id, new CreateItem("Sala", "Paredes"));
        store.UpdateItem(inspection.Id, item1.Id, new UpdateItem(ConditionStatus.Bom, ""));

        Assert.True(store.RemoveItem(inspection.Id, item2.Id));
        Assert.Equal(100, store.FindInspection(inspection.Id)!.Completion);
    }

    [Fact]
    public void CompleteInspection_WithZeroItems_Is100Percent()
    {
        var store = NewStore();
        var inspection = NewBlankInspection(store);
        var completed = store.CompleteInspection(inspection.Id, "Ana Ribeiro");
        Assert.NotNull(completed);
        Assert.Equal(100, completed!.Completion);
        Assert.Equal(InspectionStatus.EmRevisao, completed.Status);
    }

    [Fact]
    public void CompleteInspection_UnknownId_ReturnsNull()
    {
        var store = NewStore();
        Assert.Null(store.CompleteInspection(Guid.NewGuid(), "Ana Ribeiro"));
    }

    [Fact]
    public void RemoveTemplate_RefusesToRemoveASystemTemplate()
    {
        var store = NewStore();
        var systemTemplate = store.Templates.First(t => t.IsSystem);
        Assert.False(store.RemoveTemplate(systemTemplate.Id));
        Assert.Contains(store.Templates, t => t.Id == systemTemplate.Id);
    }

    [Fact]
    public void RemoveTemplate_RemovesACustomTemplate()
    {
        var store = NewStore();
        var custom = store.AddTemplate(new CreateTemplate("Modelo custom", "desc", null, [new TemplateRoom("Sala", ["Piso"])]));
        Assert.True(store.RemoveTemplate(custom.Id));
        Assert.DoesNotContain(store.Templates, t => t.Id == custom.Id);
    }

    [Fact]
    public void AddEvidence_HashIsReproducibleFromTheSameImageBytes()
    {
        var store = NewStore();
        var inspection = NewBlankInspection(store);
        var dataUrl = "data:image/png;base64," + Convert.ToBase64String([1, 2, 3, 4, 5]);

        var evidence = store.AddEvidence(inspection.Id, new CreateEvidence(null, "Sala", dataUrl, null, null, null), "Ana Ribeiro");

        Assert.Equal(DemoVistoraStore.ComputeIntegrityHash(dataUrl), evidence.IntegrityHash);
        Assert.StartsWith("SHA-256:", evidence.IntegrityHash);
        Assert.Equal(72, evidence.IntegrityHash.Length); // "SHA-256:" (8) + 64 hex chars, sem truncar
    }

    [Fact]
    public void ComputeIntegrityHash_DifferentImageBytesProduceDifferentHashes()
    {
        var a = DemoVistoraStore.ComputeIntegrityHash("data:image/png;base64," + Convert.ToBase64String([1, 2, 3]));
        var b = DemoVistoraStore.ComputeIntegrityHash("data:image/png;base64," + Convert.ToBase64String([1, 2, 4]));
        Assert.NotEqual(a, b);
    }

    [Fact]
    public void ComputeIntegrityHash_IsStableAcrossRepeatedCalls()
    {
        // Ao contrário da implementação antiga (que misturava DateTime.UtcNow no cálculo),
        // o mesmo conteúdo precisa produzir sempre o mesmo hash para servir de prova de integridade.
        var dataUrl = "data:image/jpeg;base64," + Convert.ToBase64String([9, 8, 7, 6]);
        var first = DemoVistoraStore.ComputeIntegrityHash(dataUrl);
        var second = DemoVistoraStore.ComputeIntegrityHash(dataUrl);
        Assert.Equal(first, second);
    }

    [Fact]
    public void AddEvidence_IncrementsPhotoCountOnTheLinkedItem()
    {
        var store = NewStore();
        var inspection = NewBlankInspection(store);
        var item = store.AddItem(inspection.Id, new CreateItem("Sala", "Piso"));
        var dataUrl = "data:image/png;base64," + Convert.ToBase64String([1, 2, 3]);

        store.AddEvidence(inspection.Id, new CreateEvidence(item.Id, "Sala", dataUrl, null, null, null), "Ana Ribeiro");

        var updated = store.GetItems(inspection.Id).Single(i => i.Id == item.Id);
        Assert.Equal(1, updated.PhotoCount);
    }
}
