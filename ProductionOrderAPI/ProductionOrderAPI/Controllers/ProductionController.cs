using Microsoft.AspNetCore.Mvc;
using OfficeOpenXml; // Biblioteca EPPlus para ler arquivos Excel
using ProductionOrderAPI.Models;

[ApiController]
[Route("api/[controller]")]
public class ProductionController : ControllerBase
{
    private static List<ProductionOrder> orders = new List<ProductionOrder>(); // Lista para armazenar as ordens de produção
    private static List<Apontamento> apontamentos = new List<Apontamento>(); // Lista para armazenar os apontamentos

    // Método para carregar ordens de produção a partir de um arquivo Excel
    [HttpPost("import-orders")]
    public async Task<IActionResult> ImportOrders([FromBody] string filePath)
    {
        ExcelPackage.LicenseContext = LicenseContext.NonCommercial;

        var package = new ExcelPackage(new FileInfo(filePath));
        var worksheet = package.Workbook.Worksheets[0];

        int importedOrders = 0; // Contador para saber quantas ordens foram importadas
        int orderIdCounter = 1; // Contador para gerar OrderId único para cada ordem

        // Itera sobre as linhas da planilha, começando na linha 2 (assumindo que a primeira linha contém cabeçalhos)
        for (int row = 2; row <= worksheet.Dimension.End.Row; row++)
        {
            var orderNumber = int.Parse(worksheet.Cells[row, 1].Value.ToString());
            var operationNumber = int.Parse(worksheet.Cells[row, 2].Value.ToString());
            var quantity = double.Parse(worksheet.Cells[row, 3].Value.ToString());
            var dueDate = DateTime.Parse(worksheet.Cells[row, 4].Value.ToString());
            var product = worksheet.Cells[row, 6].Value.ToString();

            // Verifica se já existe uma ordem com esse número e operação
            var existingOrder = orders.FirstOrDefault(o => o.OrderNumber == orderNumber && o.OperationNumber == operationNumber);
            if (existingOrder == null)
            {
                // Se não existir, cria uma nova ordem e adiciona à lista
                var order = new ProductionOrder
                {
                    OrderId = orderIdCounter++, // Atribui um ID incremental
                    OrderNumber = orderNumber,
                    OperationNumber = operationNumber,
                    Quantity = quantity,
                    DueDate = dueDate,
                    Product = product
                };
                orders.Add(order);
                importedOrders++; // Incrementa o contador de ordens importadas
            }
        }

        return Ok(new { ImportedOrders = importedOrders }); // Retorna o número de ordens importadas
    }

    // Método para carregar apontamentos de produção a partir de um arquivo Excel
    [HttpPost("import-apontamentos")]
    public async Task<IActionResult> ImportApontamentos([FromBody] string filePath)
    {
        ExcelPackage.LicenseContext = LicenseContext.NonCommercial;

        var package = new ExcelPackage(new FileInfo(filePath));
        var worksheet = package.Workbook.Worksheets[0];

        int importedApontamentos = 0; // Contador de apontamentos importados

        // Itera sobre as linhas da planilha, começando na linha 2
        for (int row = 2; row <= worksheet.Dimension.End.Row; row++)
        {
            var orderNumber = int.Parse(worksheet.Cells[row, 1].Value.ToString());
            var operationNumber = int.Parse(worksheet.Cells[row, 2].Value.ToString());
            var quantity = double.Parse(worksheet.Cells[row, 3].Value.ToString());

            // Verifica se o valor da célula de ProductionDateTime é nulo ou inválido
            DateTime productionDate;
            var dateCellValue = worksheet.Cells[row, 4].Value?.ToString();

            // Se a célula não contiver uma data válida, usamos a data default
            if (!DateTime.TryParse(dateCellValue, out productionDate))
            {
                productionDate = DateTime.MinValue; // Atribui a data default se a célula for nula ou inválida
            }

            // Cria um novo apontamento e adiciona à lista de apontamentos
            var apontamento = new Apontamento
            {
                OrderNumber = orderNumber,
                OperationNumber = operationNumber,
                Quantity = quantity,
                ProductionDateTime = productionDate
            };
            apontamentos.Add(apontamento);
            importedApontamentos++; // Incrementa o contador de apontamentos

            // Verifica se o valor da célula de ProductionDateTime não é nulo ou contém a string 'NULL'
        }

        return Ok(new { ImportedApontamentos = importedApontamentos });
    }

    // Método para buscar uma ordem de produção pelo OrderId
    [HttpGet("order/{orderId}")]
    public IActionResult GetOrderById(int orderId)
    {
        var order = orders.FirstOrDefault(o => o.OrderId == orderId);
        if (order == null)
        {
            return NotFound("Order not found");
        }

        // Busca todos os apontamentos relacionados à ordem encontrada
        var relatedApontamentos = apontamentos
            .Where(a => a.OrderNumber == order.OrderNumber && a.OperationNumber == order.OperationNumber)
            .ToList();

        // Retorna a ordem e seus apontamentos
        return Ok(new { Order = order, Apontamentos = relatedApontamentos });
    }

    // Método para limpar todas as ordens e apontamentos da memória
    [HttpDelete("clear-data")]
    public IActionResult ClearData()
    {
        orders.Clear(); // Limpa a lista de ordens
        apontamentos.Clear(); // Limpa a lista de apontamentos

        return NoContent(); // Retorna um status 204 (No Content)
    }

    /*
    // Método para processar as regras de negócio
    [HttpDelete("process-business-rules")]
    public IActionResult ProcessBusinessRules()
    {
        // Regras de negócio:
        // 1. Deletar ordens com quantidade apontada maior ou igual à quantidade original
        var ordersToDelete = new List<ProductionOrder>();
        var ordersToUpdate = new List<ProductionOrder>();
        var falhaDeApontamentos = new List<string>(); // Lista para armazenar as ordens que não existem no Excel de ordens


        foreach (var order in orders.ToList())
        {
            // Calcula a soma das quantidades apontadas para a ordem
            var totalApontado = apontamentos
                .Where(a => a.OrderNumber == order.OrderNumber && a.OperationNumber == order.OperationNumber)
                .Sum(a => a.Quantity);

            if (totalApontado >= order.Quantity)
            {
                // Deleta ordens com quantidade apontada maior ou igual à original
                ordersToDelete.Add(order);
            }
            else if (totalApontado > 0)
            {
                // Atualiza as ordens que têm quantidades apontadas menores que a original
                order.Quantity = totalApontado;
                ordersToUpdate.Add(order);
            }
        }

        // Remove as ordens que precisam ser deletadas
        orders.RemoveAll(o => ordersToDelete.Contains(o));

        // Regra de negócio 3: Encontrar apontamentos que não possuem ordens correspondentes
        foreach (var apontamento in apontamentos)
        {
            // Verifica se a ordem correspondente ao apontamento existe
            var existingOrder = orders.FirstOrDefault(o => o.OrderNumber == apontamento.OrderNumber && o.OperationNumber == apontamento.OperationNumber);
            if (existingOrder == null)
            {
                // Se a ordem não existir, adiciona à lista de falhas de apontamento
                falhaDeApontamentos.Add($"OrderNumber: {apontamento.OrderNumber}, OperationNumber: {apontamento.OperationNumber}");
            }
        }

        // Retorna os resultados das regras de negócio
        return Ok(new
        {
            DeletedOrders = ordersToDelete.Count,
            UpdatedOrders = ordersToUpdate.Count,
            FalhaDeApontamentos = falhaDeApontamentos // Lista de ordens que falharam no apontamento
        });
    }
    */

    // Endpoint para a Regra de Negócio 1: Deletar ordens com quantidade apontada maior ou igual à original
    [HttpDelete("rule-1-delete-orders")]
    public IActionResult DeleteOrdersWithHigherOrEqualQuantity()
    {
        var ordersToDelete = new List<ProductionOrder>();

        foreach (var order in orders.ToList())
        {
            var totalApontado = apontamentos
                .Where(a => a.OrderNumber == order.OrderNumber && a.OperationNumber == order.OperationNumber)
                .Sum(a => a.Quantity);

            if (totalApontado >= order.Quantity)
            {
                ordersToDelete.Add(order); // Adiciona a ordem à lista para ser deletada
            }
        }

        // Remove as ordens que devem ser deletadas
        orders.RemoveAll(o => ordersToDelete.Contains(o));

        return Ok(new { DeletedOrders = ordersToDelete.Count });
    }

    // Endpoint para a Regra de Negócio 2: Atualizar as ordens com soma de quantidade apontada inferior à original
    [HttpPut("rule-2-update-orders")]
    public IActionResult UpdateOrdersWithLowerQuantity()
    {
        var ordersToUpdate = new List<ProductionOrder>();

        foreach (var order in orders.ToList())
        {
            var totalApontado = apontamentos
                .Where(a => a.OrderNumber == order.OrderNumber && a.OperationNumber == order.OperationNumber)
                .Sum(a => a.Quantity);

            if (totalApontado > 0 && totalApontado < order.Quantity)
            {
                order.Quantity = totalApontado; // Atualiza a quantidade da ordem
                ordersToUpdate.Add(order);
            }
        }

        return Ok(new { UpdatedOrders = ordersToUpdate.Count });
    }

    // Endpoint para a Regra de Negócio 3: Encontrar apontamentos que não possuem ordens correspondentes
    [HttpGet("rule-3-failed-apontamentos")]
    public IActionResult ListFailedApontamentos()
    {
        var falhaDeApontamentos = new List<string>();

        foreach (var apontamento in apontamentos)
        {
            var existingOrder = orders.FirstOrDefault(o => o.OrderNumber == apontamento.OrderNumber && o.OperationNumber == apontamento.OperationNumber);
            if (existingOrder == null)
            {
                falhaDeApontamentos.Add($"OrderNumber: {apontamento.OrderNumber}, OperationNumber: {apontamento.OperationNumber}");
            }
        }

        return Ok(new { FalhaDeApontamentos = falhaDeApontamentos });
    }
    /*[HttpGet("rule-3-failed-apontamentos")]
    public IActionResult ListFailedApontamentos()
    {
        var falhaDeApontamentos = new List<string>();

        // Criamos um HashSet para armazenar combinações únicas de OrderNumber e OperationNumber de orders
        var orderHashSet = new HashSet<(int OrderNumber, int OperationNumber)>(
            orders.Select(o => (o.OrderNumber, o.OperationNumber))
        );

        // Agora iteramos pelos apontamentos e verificamos se há uma ordem correspondente
        foreach (var apontamento in apontamentos)
        {
            var key = (apontamento.OrderNumber, apontamento.OperationNumber);

            // Se a combinação de OrderNumber e OperationNumber não estiver no HashSet, é uma falha
            if (!orderHashSet.Contains(key))
            {
                falhaDeApontamentos.Add($"OrderNumber: {apontamento.OrderNumber}, OperationNumber: {apontamento.OperationNumber}");
            }
        }

        // Retornamos a lista de falhas de apontamento
        return Ok(new { FalhaDeApontamentos = falhaDeApontamentos });
    }*/

}
