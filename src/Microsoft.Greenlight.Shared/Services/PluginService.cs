// File: Microsoft.Greenlight.API.Main/Services/PluginService.cs

using Microsoft.EntityFrameworkCore;
using Microsoft.Greenlight.Shared.Data.Sql;
using Microsoft.Greenlight.Shared.Models.Plugins;

namespace Microsoft.Greenlight.Shared.Services
{
    /// <summary>
    /// Service for managing plugins and their associations with document processes.
    /// </summary>
    public class PluginService : IPluginService
    {
        private readonly DocGenerationDbContext _dbContext;

        /// <summary>
        /// Initializes a new instance of the <see cref="PluginService"/> class.
        /// </summary>
        /// <param name="dbContext">The database context.</param>
        public PluginService(DocGenerationDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        /// <inheritdoc/>
        public async Task AssociatePluginWithDocumentProcessAsync(Guid pluginId, Guid documentProcessId, string version)
        {
            // Retrieve the plugin and document process
            var plugin = await _dbContext.DynamicPlugins.FindAsync(pluginId);

            var documentProcess = await _dbContext.DynamicDocumentProcessDefinitions
                .Include(dp => dp.Plugins)
                .FirstOrDefaultAsync(dp => dp.Id == documentProcessId);

            if (plugin == null || documentProcess == null)
            {
                throw new InvalidOperationException("Plugin or Document Process not found.");
            }

            // Find the version in the plugin.Versions collection
            var versionParts = version.Split('.');
            if (versionParts.Length != 3 || !int.TryParse(versionParts[0], out var major) || !int.TryParse(versionParts[1], out var minor) || !int.TryParse(versionParts[2], out var patch))
            {
                throw new InvalidOperationException("Invalid version format.");
            }

            var versionObj = new DynamicPluginVersion(major, minor, patch);
            if (!plugin.Versions.Any(v => v.Major == versionObj.Major && v.Minor == versionObj.Minor && v.Patch == versionObj.Patch))
            {
                throw new InvalidOperationException("Specified plugin version does not exist.");
            }

            //Call the other method
            await AssociatePluginWithDocumentProcessAsync(pluginId, documentProcessId, versionObj);
        }

        /// <inheritdoc/>
        public async Task AssociatePluginWithDocumentProcessAsync(Guid pluginId, Guid documentProcessId, DynamicPluginVersion version)
        {
            // Retrieve the plugin and document process
            var plugin = await _dbContext.DynamicPlugins
                .FindAsync(pluginId);

            var documentProcess = await _dbContext.DynamicDocumentProcessDefinitions
                .Include(dp => dp.Plugins)
                .FirstOrDefaultAsync(dp => dp.Id == documentProcessId);

            if (plugin == null || documentProcess == null)
            {
                throw new InvalidOperationException("Plugin or Document Process not found.");
            }

            // Validate the version
            if (!plugin.Versions.Any(v => v.Major == version.Major && v.Minor == version.Minor && v.Patch == version.Patch))
            {
                throw new InvalidOperationException("Specified plugin version does not exist.");
            }

            // Check if association already exists
            var existingAssociation = documentProcess.Plugins!.FirstOrDefault(p => p.DynamicPluginId == pluginId);

            if (existingAssociation != null)
            {
                // Update the version
                existingAssociation.Version = version;
                _dbContext.DynamicPluginDocumentProcesses.Update(existingAssociation);
            }
            else
            {
                // Create a new association
                var association = new DynamicPluginDocumentProcess()
                {
                    DynamicPluginId = pluginId,
                    DynamicDocumentProcessDefinitionId = documentProcessId,
                    Version = version,
                };

                documentProcess.Plugins!.Add(association);
            }

            await _dbContext.SaveChangesAsync();
        }

        /// <inheritdoc/>
        public async Task DisassociatePluginFromDocumentProcessAsync(Guid pluginId, Guid documentProcessId)
        {
            // Retrieve the association
            var association = await _dbContext.DynamicPluginDocumentProcesses
                .FirstOrDefaultAsync(a => a.DynamicPluginId == pluginId && a.DynamicDocumentProcessDefinitionId == documentProcessId);

            if (association == null)
            {
                // Association does not exist
                return;
            }

            // Remove the association
            _dbContext.DynamicPluginDocumentProcesses.Remove(association);
            await _dbContext.SaveChangesAsync();
        }

        /// <inheritdoc/>
        public async Task<List<DynamicPlugin>> GetAllPluginsAsync()
        {
            var plugins = await _dbContext.DynamicPlugins
                .Include(p => p.DocumentProcesses)
                    .ThenInclude(dp => dp.DynamicDocumentProcessDefinition)
                .AsNoTracking()
                .AsSplitQuery()
                .ToListAsync();
            return plugins;
        }

        /// <inheritdoc/>
        public async Task<DynamicPlugin?> GetPluginByIdAsync(Guid pluginId)
        {
            var plugins = await _dbContext.DynamicPlugins
                 .Include(p => p.DocumentProcesses)
                     .ThenInclude(dp => dp.DynamicDocumentProcessDefinition)
                 .AsNoTracking()
                 .AsSplitQuery()
                 .FirstOrDefaultAsync(p => p.Id == pluginId);

            return plugins;
        }

        /// <inheritdoc/>
        public async Task<List<DynamicPlugin>> GetPluginsByDocumentProcessIdAsync(Guid documentProcessId)
        {
            var plugins = await _dbContext.DynamicPluginDocumentProcesses
                .Include(p => p.DynamicPlugin)
                .Where(p => p.DynamicDocumentProcessDefinitionId == documentProcessId)
                .AsNoTracking()
                .AsSplitQuery()
                .Select(p => p.DynamicPlugin)
                .ToListAsync();

            return plugins.Count == 0 ? new List<DynamicPlugin>() : plugins!;
        }
    }
}
