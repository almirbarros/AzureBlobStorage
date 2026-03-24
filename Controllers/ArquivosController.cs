using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Azure.Storage.Blobs;

namespace AzureBlobStorageAPI.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class ArquivosController : ControllerBase
    {
        private readonly string _connectionString;
        private readonly string _containerName;

        public ArquivosController(IConfiguration configuration)
        {
            _connectionString = configuration.GetSection("BlobStorage:ConnectionString").Value;
            _containerName = configuration.GetSection("BlobStorage:ContainerName").Value;
        }

        [HttpPost("upload")]
        public async Task<IActionResult> UploadArquivo(IFormFile file)
        {
            if (file == null || file.Length == 0)
                return BadRequest("Nenhum arquivo selecionado.");

            try
            {
                // 1. Criar cliente do contêiner
                var containerClient = new BlobContainerClient(_connectionString, _containerName);

                // 2. Garantir que o contêiner existe
                await containerClient.CreateIfNotExistsAsync();

                // 3. Obter referência do blob
                string fileName = Guid.NewGuid().ToString() + Path.GetExtension(file.FileName);
                var blobClient = containerClient.GetBlobClient(fileName);

                // 4. Upload do stream do arquivo
                using (var stream = file.OpenReadStream())
                {
                    await blobClient.UploadAsync(stream, overwrite: true);
                }

                return Ok(new { Url = blobClient.Uri.ToString() });
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Erro ao fazer upload do arquivo: {ex.Message}");
            }
        }

        [HttpGet("download/{fileName}")]
        public async Task<IActionResult> DownloadArquivo(string fileName)
        {
            try
            {
                var containerClient = new BlobContainerClient(_connectionString, _containerName);
                var blobClient = containerClient.GetBlobClient(fileName);

                if (!await blobClient.ExistsAsync())
                    return NotFound("Arquivo não encontrado.");

                var stream = await blobClient.OpenReadAsync();
                return File(stream, "application/octet-stream", fileName);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Erro ao baixar o arquivo: {ex.Message}");
            }
        }

        [HttpDelete("delete/{fileName}")]
        public async Task<IActionResult> DeleteArquivo(string fileName)
        {
            try
            {
                var containerClient = new BlobContainerClient(_connectionString, _containerName);
                var blobClient = containerClient.GetBlobClient(fileName);

                if (!await blobClient.ExistsAsync())
                    return NotFound("Arquivo não encontrado.");

                await blobClient.DeleteAsync();
                return Ok("Arquivo deletado com sucesso.");
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Erro ao deletar o arquivo: {ex.Message}");
            }
        }

        [HttpGet("list")]
        public async Task<IActionResult> ListarArquivos()
        {
            try
            {
                var containerClient = new BlobContainerClient(_connectionString, _containerName);
                var blobs = containerClient.GetBlobsAsync();
                var fileList = new List<BlobDto>();

                await foreach (var blob in blobs)
                {
                    fileList.Add(
                        new BlobDto
                        {
                            Nome = blob.Name,
                            Tipo = blob.Properties.ContentType,
                            Url = containerClient.Uri + "/" + blob.Name
                        });
                }

                return Ok(fileList);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Erro ao listar os arquivos: {ex.Message}");
            }
        }
    }
}