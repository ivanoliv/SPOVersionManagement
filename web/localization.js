// SPO Version Management Dashboard - Localization
// Supported languages: en (English), pt (Português)

const LOCALIZATION = {
    // ==================== HEADER ====================
    "header.title": {
        en: "SharePoint Online Optimization - Version Management",
        pt: "SharePoint Online Optimization - Version Management"
    },
    "header.subtitle": {
        en: "Live Dashboard - Auto-refresh every {0} seconds",
        pt: "Dashboard em Tempo Real - Atualização automática a cada {0} segundos"
    },
    "header.subtitlePrefix": {
        en: "Live Dashboard - Auto-refresh every",
        pt: "Dashboard em Tempo Real - Atualização automática a cada"
    },
    "header.subtitleSuffix": {
        en: "seconds",
        pt: "segundos"
    },

    // ==================== NAVIGATION TABS ====================
    "nav.overview": {
        en: "Overview",
        pt: "Visão Geral"
    },
    "nav.sites": {
        en: "SharePoint Sites",
        pt: "Sites SharePoint"
    },
    "nav.archive": {
        en: "Archive",
        pt: "Arquivamento"
    },
    "nav.excluded": {
        en: "Excluded",
        pt: "Excluídos"
    },
    "nav.metrics": {
        en: "Metrics",
        pt: "Métricas"
    },
    "nav.settings": {
        en: "Settings",
        pt: "Configurações"
    },

    // ==================== OVERVIEW TAB ====================
    "overview.tenant.title": {
        en: "Tenant Storage",
        pt: "Storage do Tenant"
    },
    "overview.tenant.total": {
        en: "Total Storage",
        pt: "Storage Total"
    },
    "overview.tenant.used": {
        en: "Used",
        pt: "Utilizado"
    },
    "overview.tenant.versions": {
        en: "Versions",
        pt: "Versões"
    },
    "overview.tenant.available": {
        en: "Available",
        pt: "Disponível"
    },
    "overview.jobs.active": {
        en: "Active Jobs",
        pt: "Jobs Ativos"
    },
    "overview.jobs.queued": {
        en: "Queued Sites",
        pt: "Sites na Fila"
    },
    "overview.jobs.completed": {
        en: "Completed",
        pt: "Concluídos"
    },
    "overview.jobs.freed": {
        en: "Space Freed",
        pt: "Espaço Liberado"
    },
    "overview.queue.title": {
        en: "Queue",
        pt: "Fila"
    },
    "overview.queue.sync": {
        en: "Sync",
        pt: "Sync"
    },
    "overview.queue.delete": {
        en: "Delete",
        pt: "Delete"
    },
    "overview.queue.all": {
        en: "All",
        pt: "Todos"
    },

    // ==================== RETENTION OVERVIEW ====================
    "overview.retention.title": {
        en: "Retention Policy Management Active",
        pt: "Gerenciamento de Política de Retenção Ativo"
    },
    "overview.retention.enabled": {
        en: "Retention policy management enabled for this run",
        pt: "Gerenciamento de política de retenção habilitado para esta execução"
    },
    "overview.retention.suspended": {
        en: "Suspended",
        pt: "Suspensos"
    },
    "overview.retention.capacity": {
        en: "Capacity",
        pt: "Capacidade"
    },
    "overview.retention.pendingSites": {
        en: "Policies suspended for",
        pt: "Políticas suspensas para"
    },

    // ==================== ACTIVE JOBS SECTION ====================
    "jobs.title": {
        en: "Active Jobs",
        pt: "Jobs Ativos"
    },
    "jobs.sync.description": {
        en: "Syncing version policies with tenant settings",
        pt: "Sincronizando políticas de versão com as configurações do tenant"
    },
    "jobs.delete.description": {
        en: "Deleting old versions (keeping {0} major versions and {1} with minor)",
        pt: "Deletando versões antigas (mantendo {0} versões principais e {1} com secundárias)"
    },
    "jobs.status.running": {
        en: "Running",
        pt: "Executando"
    },
    "jobs.status.completed": {
        en: "Completed",
        pt: "Concluído"
    },
    "jobs.status.failed": {
        en: "Failed",
        pt: "Falhou"
    },
    "jobs.status.queued": {
        en: "Queued",
        pt: "Na Fila"
    },
    "jobs.workitem": {
        en: "WorkItem",
        pt: "WorkItem"
    },
    "jobs.started": {
        en: "Started",
        pt: "Iniciado"
    },
    "jobs.storage": {
        en: "Storage",
        pt: "Storage"
    },
    "jobs.versions": {
        en: "Versions",
        pt: "Versões"
    },
    "jobs.versionSize": {
        en: "Version Size",
        pt: "Tam. Versões"
    },
    "jobs.modified": {
        en: "Modified",
        pt: "Modificado"
    },
    "jobs.noActive": {
        en: "No active jobs at the moment",
        pt: "Nenhum job ativo no momento"
    },
    "jobs.noQueued": {
        en: "No sites in queue",
        pt: "Nenhum site na fila"
    },

    // ==================== COMPLETED JOBS ====================
    "completed.title": {
        en: "Recently Completed",
        pt: "Concluídos Recentemente"
    },
    "completed.site": {
        en: "Site",
        pt: "Site"
    },
    "completed.type": {
        en: "Type",
        pt: "Tipo"
    },
    "completed.status": {
        en: "Status",
        pt: "Status"
    },
    "completed.duration": {
        en: "Duration",
        pt: "Duração"
    },
    "completed.versionsDeleted": {
        en: "Versions Deleted",
        pt: "Versões Deletadas"
    },
    "completed.spaceFreed": {
        en: "Space Freed",
        pt: "Espaço Liberado"
    },
    "completed.noJobs": {
        en: "No completed jobs yet",
        pt: "Nenhum job concluído ainda"
    },

    // ==================== SITES LIST TAB ====================
    "sites.title": {
        en: "SharePoint Sites List",
        pt: "Lista de Sites SharePoint"
    },
    "sites.search": {
        en: "Search sites...",
        pt: "Pesquisar sites..."
    },
    "sites.filter.status": {
        en: "Status",
        pt: "Status"
    },
    "sites.filter.all": {
        en: "All",
        pt: "Todos"
    },
    "sites.filter.active": {
        en: "Active",
        pt: "Ativos"
    },
    "sites.filter.archived": {
        en: "Archived",
        pt: "Arquivados"
    },
    "sites.filter.locked": {
        en: "Locked",
        pt: "Bloqueados"
    },
    "sites.filter.activeUnlocked": {
        en: "Active (unlocked)",
        pt: "Ativos (desbloqueados)"
    },
    "sites.filter.notArchived": {
        en: "All minus Archived",
        pt: "Todos menos Arquivados"
    },
    "sites.filter.withVersions": {
        en: "With versions > 0",
        pt: "Com versões > 0"
    },
    "sites.filter.hub": {
        en: "Hub Type",
        pt: "Tipo Hub"
    },
    "sites.filter.hubSite": {
        en: "Hub Site",
        pt: "Hub Site"
    },
    "sites.filter.connected": {
        en: "Connected to Hub",
        pt: "Conectado a Hub"
    },
    "sites.filter.noHub": {
        en: "No Hub",
        pt: "Sem Hub"
    },
    "sites.filter.policy": {
        en: "Version Policy",
        pt: "Política de Versão"
    },
    "sites.filter.tenant": {
        en: "Tenant Policy",
        pt: "Política Tenant"
    },
    "sites.filter.custom": {
        en: "Custom Policy",
        pt: "Política Custom"
    },
    "sites.refresh": {
        en: "Refresh List",
        pt: "Atualizar Lista"
    },
    "sites.export": {
        en: "Export CSV",
        pt: "Exportar CSV"
    },
    "sites.processSelected": {
        en: "Process Selected",
        pt: "Processar Selecionados"
    },
    "sites.excludeSelected": {
        en: "Exclude Selected",
        pt: "Excluir Selecionados"
    },
    "sites.column.select": {
        en: "Select",
        pt: "Selecionar"
    },
    "sites.column.title": {
        en: "Title",
        pt: "Título"
    },
    "sites.column.url": {
        en: "URL",
        pt: "URL"
    },
    "sites.column.storage": {
        en: "Storage",
        pt: "Storage"
    },
    "sites.column.versions": {
        en: "Versions",
        pt: "Versões"
    },
    "sites.column.versionSize": {
        en: "Version Size",
        pt: "Tam. Versões"
    },
    "sites.column.template": {
        en: "Template",
        pt: "Template"
    },
    "sites.column.status": {
        en: "Status",
        pt: "Status"
    },
    "sites.column.archiveStatus": {
        en: "Archive",
        pt: "Arquivo"
    },
    "sites.column.lockState": {
        en: "Lock",
        pt: "Bloqueio"
    },
    "sites.column.lastModified": {
        en: "Last Modified",
        pt: "Última Modif."
    },
    "sites.column.created": {
        en: "Created",
        pt: "Criado"
    },
    "sites.column.owner": {
        en: "Owner",
        pt: "Proprietário"
    },
    "sites.column.policy": {
        en: "Policy",
        pt: "Política"
    },
    "sites.column.actions": {
        en: "Actions",
        pt: "Ações"
    },
    "sites.summary.total": {
        en: "Total Sites",
        pt: "Total de Sites"
    },
    "sites.summary.storage": {
        en: "Total Storage",
        pt: "Storage Total"
    },
    "sites.summary.versions": {
        en: "Total Versions",
        pt: "Total Versões"
    },
    "sites.summary.lastUpdate": {
        en: "Last Update",
        pt: "Última Atualização"
    },
    "sites.noSites": {
        en: "No sites loaded",
        pt: "Nenhum site carregado"
    },
    "sites.noMatch": {
        en: "No sites match the filters",
        pt: "Nenhum site corresponde aos filtros"
    },
    "sites.addedToExclusion": {
        en: "added to exclusion list!",
        pt: "adicionado(s) à lista de exclusão!"
    },
    "sites.addedToArchive": {
        en: "site(s) added to archive queue!",
        pt: "site(s) adicionado(s) à fila de arquivo!"
    },
    "sites.buttons.refresh": {
        en: "Refresh List",
        pt: "Atualizar Lista"
    },
    "sites.buttons.addToExclusion": {
        en: "Add to Exclusion",
        pt: "Adicionar à Exclusão"
    },
    "sites.buttons.addToArchive": {
        en: "Add to Archive",
        pt: "Adicionar ao Arquivo"
    },
    "sites.buttons.export": {
        en: "Export CSV",
        pt: "Exportar CSV"
    },
    "sites.summary.versionSize": {
        en: "Version Size:",
        pt: "Tamanho Versões:"
    },
    "sites.summary.versionPct": {
        en: "Version %",
        pt: "Versões %"
    },
    "sites.summary.totalVersions": {
        en: "Total Versions:",
        pt: "Versões Total:"
    },
    "sites.search.placeholder": {
        en: "🔍 Filter by URL, title or owner...",
        pt: "🔍 Filtrar por URL, título ou owner..."
    },
    "sites.filter.isHub": {
        en: "Is Hub Site",
        pt: "É Hub Site"
    },
    "sites.filter.allPolicies": {
        en: "All",
        pt: "Todas"
    },
    "sites.filter.tenantInherit": {
        en: "Inherits from Tenant",
        pt: "Herda do Tenant"
    },
    "sites.filter.customPolicy": {
        en: "Custom",
        pt: "Customizada"
    },
    "sites.column.hub": {
        en: "Hub",
        pt: "Hub"
    },
    "sites.column.subsites": {
        en: "Subsites",
        pt: "Subsites"
    },
    "sites.column.quota": {
        en: "Quota",
        pt: "Quota"
    },
    "sites.column.localeId": {
        en: "LocaleId",
        pt: "LocaleId"
    },
    "sites.column.siteId": {
        en: "SiteId",
        pt: "SiteId"
    },
    "sites.empty.clickRefresh": {
        en: "Click \"Refresh List\" to load sites",
        pt: "Clique em \"Atualizar Lista\" para carregar os sites"
    },
    "sites.empty.runExport": {
        en: "Run Export-AllSitesDataForDashboard in PowerShell to generate the data",
        pt: "Execute Export-AllSitesDataForDashboard no PowerShell para gerar os dados"
    },
    "sites.pagination.showing": {
        en: "Showing {0}-{1} of {2}",
        pt: "Mostrando {0}-{1} de {2}"
    },
    "sites.pagination.prev": {
        en: "◀ Previous",
        pt: "◀ Anterior"
    },
    "sites.pagination.next": {
        en: "Next ▶",
        pt: "Próximo ▶"
    },
    "sites.pagination.perPage": {
        en: "Per page:",
        pt: "Por página:"
    },

    // ==================== ARCHIVE TAB ====================
    "archive.title": {
        en: "Archive Candidates",
        pt: "Candidatos a Arquivamento"
    },
    "archive.description": {
        en: "Sites with no activity in the last {0} days",
        pt: "Sites sem atividade nos últimos {0} dias"
    },
    "archive.period": {
        en: "Inactivity period (days)",
        pt: "Período de inatividade (dias)"
    },
    "archive.filter": {
        en: "Filter",
        pt: "Filtrar"
    },
    "archive.selectAll": {
        en: "Select All",
        pt: "Selecionar Todos"
    },
    "archive.archiveSelected": {
        en: "Archive Selected",
        pt: "Arquivar Selecionados"
    },
    "archive.summary.sites": {
        en: "Candidate Sites",
        pt: "Sites Candidatos"
    },
    "archive.summary.storage": {
        en: "Total Storage",
        pt: "Storage Total"
    },
    "archive.summary.avgInactive": {
        en: "Avg. Inactive Days",
        pt: "Média Dias Inativos"
    },
    "archive.column.site": {
        en: "Site",
        pt: "Site"
    },
    "archive.column.daysInactive": {
        en: "Days Inactive",
        pt: "Dias Inativo"
    },
    "archive.column.storage": {
        en: "Storage",
        pt: "Storage"
    },
    "archive.column.versionCount": {
        en: "Versions",
        pt: "Versões"
    },
    "archive.column.versionSizeMB": {
        en: "Ver. Size (MB)",
        pt: "Tam. Versões"
    },
    "archive.column.versionPct": {
        en: "Ver. %",
        pt: "Ver. %"
    },
    "archive.column.lastModified": {
        en: "Last Modified",
        pt: "Última Modif."
    },
    "archive.column.created": {
        en: "Created",
        pt: "Criado"
    },
    "archive.column.owner": {
        en: "Owner",
        pt: "Proprietário"
    },
    "archive.noSites": {
        en: "No archive candidates found",
        pt: "Nenhum candidato a arquivamento encontrado"
    },
    "archive.exportedToArchive": {
        en: "sites exported for archiving!",
        pt: "sites exportados para arquivamento!"
    },
    "archive.noSitesToExport": {
        en: "No candidate sites to export.",
        pt: "Nenhum site candidato para exportar."
    },
    "archive.buttons.refresh": {
        en: "Refresh",
        pt: "Atualizar"
    },
    "archive.buttons.export": {
        en: "Export CSV",
        pt: "Exportar CSV"
    },
    "archive.info.title": {
        en: "About SharePoint Archive",
        pt: "Sobre o SharePoint Archive"
    },
    "archive.info.text": {
        en: "SharePoint Archive lets you store inactive sites at <strong>75% lower cost</strong> than standard storage. <strong>Important:</strong> You only start paying for Archive when the tenant quota is exceeded. While there's available space, archived data doesn't incur additional cost. The values below show cost projections based on your tenant's growth.",
        pt: "O SharePoint Archive permite armazenar sites inativos com custo <strong>75% menor</strong> que o storage padrão. <strong>Importante:</strong> Você só começa a pagar pelo Archive quando a quota do tenant for excedida. Enquanto houver espaço disponível, os dados arquivados não geram custo adicional. Os valores abaixo mostram a projeção de custos considerando o crescimento do seu tenant."
    },
    "archive.period.label": {
        en: "📅 Inactivity Period:",
        pt: "📅 Período de Inatividade:"
    },
    "archive.period.description": {
        en: "Sites without modification for more than",
        pt: "Sites sem modificação há mais de"
    },
    "archive.period.days": {
        en: "days",
        pt: "dias"
    },
    "archive.card.candidates": {
        en: "Candidate Sites",
        pt: "Sites Candidatos"
    },
    "archive.summary.total": {
        en: "Candidate Sites",
        pt: "Sites Candidatos"
    },
    "archive.summary.storage": {
        en: "Total Storage",
        pt: "Storage Total"
    },
    "archive.summary.versions": {
        en: "Versions",
        pt: "Versões"
    },
    "archive.summary.versionSize": {
        en: "Version Size",
        pt: "Tamanho Versões"
    },
    "archive.summary.versionPct": {
        en: "Version %",
        pt: "Versões %"
    },
    "archive.summary.hotCost": {
        en: "Hot Cost/Year",
        pt: "Custo Hot/Ano"
    },
    "archive.summary.archiveCost": {
        en: "Archive Cost/Year",
        pt: "Custo Archive/Ano"
    },
    "archive.summary.savings": {
        en: "Savings/Year",
        pt: "Economia/Ano"
    },
    "archive.card.candidatesSub": {
        en: "No activity in period",
        pt: "Sem atividade no período"
    },
    "archive.card.archivable": {
        en: "Archivable Storage",
        pt: "Storage Arquivável"
    },
    "archive.card.archivableSub": {
        en: "Would free tenant space",
        pt: "Liberaria espaço no tenant"
    },
    "archive.card.hotCost": {
        en: "Cost if Exceeded (Hot)",
        pt: "Custo se Exceder (Hot)"
    },
    "archive.card.archiveCost": {
        en: "Cost if Exceeded (Archive)",
        pt: "Custo se Exceder (Archive)"
    },
    "archive.card.savings": {
        en: "Potential Savings",
        pt: "Economia Potencial"
    },
    "archive.card.savingsSub": {
        en: "Using Archive vs Hot",
        pt: "Usando Archive vs Hot"
    },
    "archive.card.monthsUntilPay": {
        en: "Months Until Charge",
        pt: "Meses até Cobrar"
    },
    "archive.card.monthsUntilPaySub": {
        en: "Quota is OK until then",
        pt: "Quota está OK até lá"
    },
    "archive.chart.title": {
        en: "📊 Cost Projection: Hot Storage vs Archive",
        pt: "📊 Projeção de Custos: Hot Storage vs Archive"
    },
    "archive.chart.consumption": {
        en: "📈 Tenant Consumption + Projection",
        pt: "📈 Consumo do Tenant + Projeção"
    },
    "archive.chart.comparison": {
        en: "💰 Cost Comparison (Next 12 months)",
        pt: "💰 Comparativo de Custos (Próximos 12 meses)"
    },
    "archive.chart.withoutArchive": {
        en: "Without Archive",
        pt: "Sem Archive"
    },
    "archive.chart.withArchive": {
        en: "With Archive",
        pt: "Com Archive"
    },
    "archive.chart.hotStorageCost": {
        en: "Hot Storage Cost (without Archive)",
        pt: "Custo Hot Storage (sem Archive)"
    },
    "archive.chart.archiveCost": {
        en: "Cost with Archive",
        pt: "Custo com Archive"
    },
    "archive.loading": {
        en: "Loading Archive Analysis data...",
        pt: "Carregando dados de Análise de Arquivamento..."
    },
    "archive.loading.hint": {
        en: "This may take a moment for large tenants",
        pt: "Isso pode levar um momento para tenants grandes"
    },
    "archive.chart.note": {
        en: "💡 Annual cost comparison for candidate data stored as Hot vs Archive tier.",
        pt: "💡 Comparação anual de custos para os dados candidatos armazenados como tier Hot vs Archive."
    },
    "archive.comparison.withoutArchive": {
        en: "Without Archive (12 months)",
        pt: "Sem Archive (12 meses)"
    },
    "archive.comparison.withArchive": {
        en: "With Archive (12 months)",
        pt: "Com Archive (12 meses)"
    },
    "archive.comparison.projectedSavings": {
        en: "Projected Savings",
        pt: "Economia Projetada"
    },
    "archive.table.title": {
        en: "📋 Archive Candidate Sites",
        pt: "📋 Sites Candidatos ao Arquivamento"
    },
    "archive.table.markForArchive": {
        en: "Mark for Archive",
        pt: "Marcar para Archive"
    },
    "archive.column.title": {
        en: "Title",
        pt: "Título"
    },
    "archive.column.url": {
        en: "URL",
        pt: "URL"
    },
    "archive.column.storageMB": {
        en: "Storage (MB)",
        pt: "Storage (MB)"
    },
    "archive.column.daysInactive": {
        en: "Days Inactive",
        pt: "Dias Inativo"
    },
    "archive.column.lastModified": {
        en: "Last Modified",
        pt: "Última Modificação"
    },
    "archive.column.createdAt": {
        en: "Created At",
        pt: "Criado Em"
    },
    "archive.column.owner": {
        en: "Owner",
        pt: "Owner"
    },
    "archive.column.status": {
        en: "Status",
        pt: "Status"
    },
    "archive.column.lockState": {
        en: "Lock State",
        pt: "Lock State"
    },
    "archive.column.hotCostYear": {
        en: "Hot Cost/Year",
        pt: "Custo Hot/Ano"
    },
    "archive.column.archiveCostYear": {
        en: "Archive Cost/Year",
        pt: "Custo Archive/Ano"
    },
    "archive.column.savingsYear": {
        en: "Savings/Year",
        pt: "Economia/Ano"
    },
    "archive.empty.selectPeriod": {
        en: "Select an inactivity period above",
        pt: "Selecione um período de inatividade acima"
    },
    "archive.empty.runExport": {
        en: "Make sure to run Export-AllSitesDataForDashboard in PowerShell",
        pt: "Certifique-se de executar Export-AllSitesDataForDashboard no PowerShell"
    },
    "archive.archivedSites.title": {
        en: "Currently Archived Sites",
        pt: "Sites Atualmente Arquivados"
    },
    "archive.archivedSites.empty": {
        en: "No archived sites found. Load AllSites data first.",
        pt: "Nenhum site arquivado encontrado. Carregue os dados de AllSites primeiro."
    },
    "archive.archivedSites.archiveStatus": {
        en: "Archive Status",
        pt: "Status de Arquivo"
    },

    // ==================== ARCHIVE QUEUE ====================
    "archive.queue.title": {
        en: "Archive Queue",
        pt: "Fila de Arquivo"
    },
    "archive.queue.empty": {
        en: "No sites in the archive queue. Select candidates and click 'Add to Archive' to queue them.",
        pt: "Nenhum site na fila de arquivo. Selecione candidatos e clique em 'Adicionar ao Arquivo' para enfileirá-los."
    },
    "archive.queue.generateCommand": {
        en: "Generate PowerShell Command",
        pt: "Gerar Comando PowerShell"
    },
    "archive.queue.clear": {
        en: "Clear Queue",
        pt: "Limpar Fila"
    },
    "archive.queue.downloadJson": {
        en: "Download ArchiveQueue.json",
        pt: "Baixar ArchiveQueue.json"
    },
    "archive.queue.sites": {
        en: "sites in queue",
        pt: "sites na fila"
    },
    "archive.queue.commandTitle": {
        en: "PowerShell Command",
        pt: "Comando PowerShell"
    },
    "archive.queue.copyCommand": {
        en: "Copy Command",
        pt: "Copiar Comando"
    },

    // ==================== ARCHIVE LEGEND ====================
    "archive.legend.queued": {
        en: "Queued for archive",
        pt: "Na fila de arquivamento"
    },
    "archive.legend.processed": {
        en: "Has processing history",
        pt: "Possui histórico de processamento"
    },
    "archive.legend.selected": {
        en: "Selected",
        pt: "Selecionado"
    },

    // ==================== SKIPPED SITES ====================
    "skipped.title": {
        en: "Skipped Sites",
        pt: "Sites Ignorados"
    },
    "skipped.exportReprocess": {
        en: "Export for Reprocess",
        pt: "Exportar para Reprocessar"
    },
    "skipped.copyUrls": {
        en: "Copy URLs",
        pt: "Copiar URLs"
    },
    "skipped.column.site": {
        en: "Site",
        pt: "Site"
    },
    "skipped.column.reason": {
        en: "Reason",
        pt: "Motivo"
    },
    "skipped.column.details": {
        en: "Details",
        pt: "Detalhes"
    },
    "skipped.column.phase": {
        en: "Phase",
        pt: "Fase"
    },
    "skipped.reason.retentionCapacity": {
        en: "Retention at Capacity",
        pt: "Retenção no Limite"
    },
    "skipped.reason.retentionCapacityDesc": {
        en: "Exception list full (100 limit) — re-queued automatically",
        pt: "Lista de exceções cheia (limite 100) — re-enfileirado automaticamente"
    },
    "skipped.reason.retentionFailed": {
        en: "Retention Suspend Failed",
        pt: "Falha na Suspensão de Retenção"
    },
    "skipped.reason.retentionFailedDesc": {
        en: "Could not suspend retention policy — skipped to protect data",
        pt: "Não foi possível suspender a política de retenção — ignorado para proteger dados"
    },
    "skipped.reason.siteBlocked": {
        en: "Site Blocked/Archived",
        pt: "Site Bloqueado/Arquivado"
    },
    "skipped.reason.siteBlockedDesc": {
        en: "Site is archived, locked, or read-only",
        pt: "Site está arquivado, bloqueado ou somente leitura"
    },
    "skipped.reason.recentlyProcessed": {
        en: "Recently Processed",
        pt: "Processado Recentemente"
    },
    "skipped.reason.recentlyProcessedDesc": {
        en: "Site was processed within re-execution interval",
        pt: "Site foi processado dentro do intervalo de re-execução"
    },
    "skipped.reason.userSkipped": {
        en: "User Skipped",
        pt: "Ignorado pelo Usuário"
    },
    "skipped.reason.userSkippedDesc": {
        en: "Skipped by user choice during execution",
        pt: "Ignorado por escolha do usuário durante execução"
    },
    "skipped.noSitesSelected": {
        en: "No sites selected for reprocess",
        pt: "Nenhum site selecionado para reprocessar"
    },
    "skipped.exportedReprocess": {
        en: "Exported {0} sites to IncludeSites_Reprocess.csv",
        pt: "Exportados {0} sites para IncludeSites_Reprocess.csv"
    },
    "skipped.copiedUrls": {
        en: "Copied {0} URLs to clipboard",
        pt: "Copiadas {0} URLs para a área de transferência"
    },

    // ==================== SAM REPORT STATS ====================
    "archive.sam.title": {
        en: "Activity Analysis & Period Breakdown",
        pt: "Análise de Atividade & Distribuição por Período"
    },
    "archive.sam.totalSites": {
        en: "Total Sites Analyzed",
        pt: "Total de Sites Analisados"
    },
    "archive.sam.inactive": {
        en: "Inactive Sites (180d+)",
        pt: "Sites Inativos (180d+)"
    },
    "archive.sam.ownerless": {
        en: "Ownerless Sites",
        pt: "Sites sem Proprietário"
    },
    "archive.sam.matchRate": {
        en: "Match Rate",
        pt: "Taxa de Correspondência"
    },
    "archive.sam.periodBreakdown": {
        en: "Period Breakdown",
        pt: "Distribuição por Período"
    },

    // ==================== EXCLUDED SITES TAB ====================
    "excluded.title": {
        en: "Excluded Sites",
        pt: "Sites Excluídos"
    },
    "excluded.heading": {
        en: "Sites Excluded from Processing",
        pt: "Sites Excluídos do Processamento"
    },
    "excluded.description": {
        en: "Sites excluded from version management processing",
        pt: "Sites excluídos do processamento de gerenciamento de versões"
    },
    "excluded.add": {
        en: "Add Site",
        pt: "Adicionar Site"
    },
    "excluded.export": {
        en: "Export JSON",
        pt: "Exportar JSON"
    },
    "excluded.warning": {
        en: "protected sites - These sites will NOT have versions deleted. Versions will be kept intact to ensure compliance and security.",
        pt: "sites protegidos - Estes sites NÃO terão versões deletadas. As versões serão mantidas intactas para garantir conformidade e segurança."
    },
    "excluded.empty": {
        en: "No sites in exclusion list",
        pt: "Nenhum site na lista de exclusão"
    },
    "excluded.emptyHint": {
        en: "Click \"Add Site\" to protect sites from processing",
        pt: "Clique em \"Adicionar Site\" para proteger sites do processamento"
    },
    "excluded.addPlaceholder": {
        en: "Enter site URL to exclude...",
        pt: "Digite a URL do site para excluir..."
    },
    "excluded.column.url": {
        en: "Site URL",
        pt: "URL do Site"
    },
    "excluded.column.addedAt": {
        en: "Added At",
        pt: "Adicionado em"
    },
    "excluded.column.reason": {
        en: "Reason",
        pt: "Motivo"
    },
    "excluded.column.actions": {
        en: "Actions",
        pt: "Ações"
    },
    "excluded.remove": {
        en: "Remove",
        pt: "Remover"
    },
    "excluded.noSites": {
        en: "No excluded sites",
        pt: "Nenhum site excluído"
    },

    // ==================== METRICS TAB ====================
    "metrics.title": {
        en: "Execution Metrics",
        pt: "Métricas de Execução"
    },
    "metrics.summary.totalExecutions": {
        en: "Total Executions",
        pt: "Total de Execuções"
    },
    "metrics.summary.versionsDeleted": {
        en: "Versions Deleted",
        pt: "Versões Deletadas"
    },
    "metrics.summary.spaceFreed": {
        en: "Space Freed",
        pt: "Espaço Liberado"
    },
    "metrics.summary.sitesProcessed": {
        en: "Sites Processed",
        pt: "Sites Processados"
    },
    "metrics.chart.versionsOverTime": {
        en: "Versions Deleted Over Time",
        pt: "Versões Deletadas ao Longo do Tempo"
    },
    "metrics.chart.spaceOverTime": {
        en: "Space Freed Over Time",
        pt: "Espaço Liberado ao Longo do Tempo"
    },
    "metrics.chart.topSites": {
        en: "Top Sites by Space Freed",
        pt: "Top Sites por Espaço Liberado"
    },

    // ==================== SETTINGS TAB ====================
    "settings.title": {
        en: "Dashboard Settings",
        pt: "Configurações do Dashboard"
    },
    "settings.display.title": {
        en: "Display Settings",
        pt: "Configurações de Exibição"
    },
    "settings.display.language": {
        en: "Language",
        pt: "Idioma"
    },
    "settings.display.theme": {
        en: "Theme",
        pt: "Tema"
    },
    "settings.display.themeDark": {
        en: "Dark",
        pt: "Escuro"
    },
    "settings.display.themeLight": {
        en: "Light",
        pt: "Claro"
    },
    "settings.display.refreshInterval": {
        en: "Auto-refresh interval (seconds)",
        pt: "Intervalo de atualização (segundos)"
    },
    "settings.display.pageSize": {
        en: "Items per page",
        pt: "Itens por página"
    },
    "settings.paths.title": {
        en: "File Paths",
        pt: "Caminhos dos Arquivos"
    },
    "settings.paths.basePath": {
        en: "Base Path",
        pt: "Caminho Base"
    },
    "settings.paths.logsFolder": {
        en: "Logs Folder",
        pt: "Pasta de Logs"
    },
    "settings.defaults.title": {
        en: "Default Values",
        pt: "Valores Padrão"
    },
    "settings.defaults.archivePeriod": {
        en: "Archive period (days)",
        pt: "Período de arquivamento (dias)"
    },
    "settings.defaults.majorVersions": {
        en: "Major versions to keep",
        pt: "Versões principais a manter"
    },
    "settings.defaults.minorVersions": {
        en: "Minor versions to keep",
        pt: "Versões secundárias a manter"
    },
    "settings.save": {
        en: "Save Settings",
        pt: "Salvar Configurações"
    },
    "settings.reset": {
        en: "Reset to Defaults",
        pt: "Restaurar Padrões"
    },
    "settings.storageCosts": {
        en: "Storage Costs",
        pt: "Custos de Storage"
    },
    "settings.archiveCosts": {
        en: "SharePoint Archive Costs",
        pt: "Custos de SharePoint Archive"
    },
    "settings.autoRefresh": {
        en: "Auto Refresh",
        pt: "Atualização Automática"
    },
    "settings.zeroVersionSites": {
        en: "Zero Version Sites",
        pt: "Sites Sem Versões"
    },
    "settings.zeroVersionSitesDesc": {
        en: "Configure how sites with VersionCount=0 and VersionSize=0 should be processed",
        pt: "Configure como sites com VersionCount=0 e VersionSize=0 devem ser processados"
    },
    "settings.zeroVersionAction": {
        en: "Processing Action",
        pt: "Ação de Processamento"
    },
    "settings.zeroVersionActionDesc": {
        en: "What to do with sites that have no versions",
        pt: "O que fazer com sites que não têm versões"
    },
    "settings.zeroVersionAsk": {
        en: "Ask each time",
        pt: "Perguntar cada vez"
    },
    "settings.zeroVersionSkip": {
        en: "Skip these sites",
        pt: "Pular estes sites"
    },
    "settings.zeroVersionSyncOnly": {
        en: "Process with Sync only",
        pt: "Processar apenas com Sync"
    },
    "settings.zeroVersionBoth": {
        en: "Process both jobs",
        pt: "Processar ambos os jobs"
    },
    "settings.zeroVersionInfo": {
        en: "Information",
        pt: "Informação"
    },
    "settings.zeroVersionInfoText": {
        en: "Sites with zero versions have no old versions to delete. Running BatchDelete on these sites is unnecessary. The recommended option is 'Sync only' which will update the version policy without attempting to delete.",
        pt: "Sites com zero versões não têm versões antigas para excluir. Executar BatchDelete nesses sites é desnecessário. A opção recomendada é 'Apenas Sync' que atualizará a política de versões sem tentar excluir."
    },
    "settings.reexecutionInterval": {
        en: "Re-execution Interval",
        pt: "Intervalo de Reexecução"
    },
    "settings.reexecutionIntervalDesc": {
        en: "Configure minimum interval between job executions for the same site",
        pt: "Configure o intervalo mínimo entre execuções de jobs para o mesmo site"
    },
    "settings.skipRecentlyProcessed": {
        en: "Skip Recently Processed Sites",
        pt: "Pular Sites Processados Recentemente"
    },
    "settings.skipRecentlyProcessedDesc": {
        en: "Skip sites that were successfully processed within this interval",
        pt: "Pular sites que foram processados com sucesso dentro deste intervalo"
    },
    "settings.reexecutionDisabled": {
        en: "Disabled (always process)",
        pt: "Desabilitado (sempre processar)"
    },
    "settings.reexecution1Day": {
        en: "1 day",
        pt: "1 dia"
    },
    "settings.reexecution2Days": {
        en: "2 days",
        pt: "2 dias"
    },
    "settings.reexecution3Days": {
        en: "3 days",
        pt: "3 dias"
    },
    "settings.reexecution4Days": {
        en: "4 days",
        pt: "4 dias"
    },
    "settings.reexecution5Days": {
        en: "5 days",
        pt: "5 dias"
    },
    "settings.reexecution6Days": {
        en: "6 days",
        pt: "6 dias"
    },
    "settings.reexecution7Days": {
        en: "7 days",
        pt: "7 dias"
    },
    "settings.reexecutionInfo": {
        en: "Information",
        pt: "Informação"
    },
    "settings.reexecutionInfoText": {
        en: "When enabled, sites that have been successfully processed (CompleteSuccess) within the configured interval will be skipped. This prevents unnecessary reprocessing and saves API calls.",
        pt: "Quando habilitado, sites que foram processados com sucesso (CompleteSuccess) dentro do intervalo configurado serão pulados. Isso evita reprocessamento desnecessário e economiza chamadas de API."
    },
    "settings.entraIdApps": {
        en: "Entra ID App Registration",
        pt: "Registro de App Entra ID"
    },
    "settings.entraIdAppsDesc": {
        en: "Configure the Entra ID application credentials used for Microsoft Graph and Purview API access",
        pt: "Configure as credenciais dos aplicativos Entra ID usados para acesso à API do Microsoft Graph e Purview"
    },
    "settings.spoApp": {
        en: "SharePoint Online App (Graph API)",
        pt: "App SharePoint Online (Graph API)"
    },
    "settings.spoTenantId": {
        en: "Tenant ID",
        pt: "Tenant ID"
    },
    "settings.spoTenantIdDesc": {
        en: "Azure AD Tenant ID (GUID)",
        pt: "ID do Tenant Azure AD (GUID)"
    },
    "settings.spoClientId": {
        en: "Client ID",
        pt: "Client ID"
    },
    "settings.spoClientIdDesc": {
        en: "Application (client) ID for SharePoint Graph API",
        pt: "ID do Aplicativo (cliente) para SharePoint Graph API"
    },
    "settings.spoCertThumbprint": {
        en: "Certificate Thumbprint",
        pt: "Impressão Digital do Certificado"
    },
    "settings.spoCertThumbprintDesc": {
        en: "Certificate thumbprint for authentication",
        pt: "Impressão digital do certificado para autenticação"
    },
    "settings.purviewApp": {
        en: "Purview App (Retention Policies)",
        pt: "App Purview (Políticas de Retenção)"
    },
    "settings.purviewClientId": {
        en: "Client ID",
        pt: "Client ID"
    },
    "settings.purviewClientIdDesc": {
        en: "Application (client) ID for Purview/Compliance API",
        pt: "ID do Aplicativo (cliente) para API Purview/Compliance"
    },
    "settings.purviewCertThumbprint": {
        en: "Certificate Thumbprint",
        pt: "Impressão Digital do Certificado"
    },
    "settings.purviewCertThumbprintDesc": {
        en: "Certificate thumbprint for Purview authentication",
        pt: "Impressão digital do certificado para autenticação Purview"
    },
    "settings.purviewOrganization": {
        en: "Organization",
        pt: "Organização"
    },
    "settings.purviewOrganizationDesc": {
        en: "Tenant domain (e.g., contoso.onmicrosoft.com)",
        pt: "Domínio do tenant (ex: contoso.onmicrosoft.com)"
    },
    "settings.entraIdAppsInfo": {
        en: "Information",
        pt: "Informação"
    },
    "settings.entraIdAppsInfoText": {
        en: "These credentials are stored in AppPaths.json and used by the PowerShell scripts for app-based authentication. The SPO App connects to Microsoft Graph API for site enumeration and version management. The Purview App connects to Security & Compliance for retention policy management.",
        pt: "Essas credenciais são armazenadas no AppPaths.json e usadas pelos scripts PowerShell para autenticação baseada em app. O App SPO conecta à API Microsoft Graph para enumeração de sites e gerenciamento de versões. O App Purview conecta ao Security & Compliance para gerenciamento de políticas de retenção."
    },
    "settings.directories": {
        en: "Application Directories",
        pt: "Diretórios da Aplicação"
    },
    "settings.costPerGBMonth": {
        en: "Cost per GB/month",
        pt: "Custo por GB/mês"
    },
    "settings.costPerGBMonthDesc": {
        en: "Base monthly value for exceeded storage cost calculations",
        pt: "Valor base mensal para cálculos de custo de storage excedente"
    },
    "settings.costPerTBMonth": {
        en: "Cost per TB/month",
        pt: "Custo por TB/mês"
    },
    "settings.costPerTBMonthDesc": {
        en: "Calculated automatically (GB x 1000)",
        pt: "Calculado automaticamente (GB x 1000)"
    },
    "settings.costPerGBYear": {
        en: "Cost per GB/year",
        pt: "Custo por GB/ano"
    },
    "settings.costPerGBYearDesc": {
        en: "Calculated automatically (month x 12)",
        pt: "Calculado automaticamente (mês x 12)"
    },
    "settings.costPerTBYear": {
        en: "Cost per TB/year",
        pt: "Custo por TB/ano"
    },
    "settings.costPerTBYearDesc": {
        en: "Calculated automatically - Annualized view",
        pt: "Calculado automaticamente - Visão anualizada"
    },
    "settings.exchangeRate": {
        en: "Exchange Rate (USD → Local Currency)",
        pt: "Taxa de Câmbio (USD → Moeda Local)"
    },
    "settings.exchangeRateDesc": {
        en: "Conversion from dollar to local currency",
        pt: "Conversão do dólar para a moeda local"
    },
    "settings.costPreview": {
        en: "🔍 Cost Preview",
        pt: "🔍 Pré-visualização de Custos"
    },
    "settings.costOf1TBMonth": {
        en: "Cost of 1 TB/month:",
        pt: "Custo de 1 TB/mês:"
    },
    "settings.costOf1TBYear": {
        en: "Cost of 1 TB/year:",
        pt: "Custo de 1 TB/ano:"
    },
    "settings.hotStorageCost": {
        en: "Hot Storage cost per GB/month (USD)",
        pt: "Custo Hot Storage por GB/mês (USD)"
    },
    "settings.hotStorageCostDesc": {
        en: "Standard SharePoint Online base cost",
        pt: "Custo base do SharePoint Online padrão"
    },
    "settings.archiveDiscount": {
        en: "Archive Discount (%)",
        pt: "Desconto Archive (%)"
    },
    "settings.archiveDiscountDesc": {
        en: "Savings percentage when using Archive tier",
        pt: "Percentual de economia ao usar Archive tier"
    },
    "settings.refreshInterval": {
        en: "Refresh interval (seconds)",
        pt: "Intervalo de atualização (segundos)"
    },
    "settings.refreshIntervalDesc": {
        en: "How often the Dashboard fetches new data",
        pt: "Frequência com que o Dashboard busca novos dados"
    },
    "settings.basePath": {
        en: "Base Path",
        pt: "Caminho Base"
    },
    "settings.basePathDesc": {
        en: "Root folder of the application",
        pt: "Pasta raiz da aplicação"
    },
    "settings.logsPath": {
        en: "Logs Folder",
        pt: "Pasta de Logs"
    },
    "settings.logsPathDesc": {
        en: "Folder where logs are stored",
        pt: "Pasta onde os logs são salvos"
    },
    "settings.dataPath": {
        en: "Data Folder",
        pt: "Pasta de Dados"
    },
    "settings.dataPathDesc": {
        en: "Folder where data files are stored",
        pt: "Pasta onde os arquivos de dados são salvos"
    },
    "metrics.trendAnalysis": {
        en: "📈 Growth Trend Analysis",
        pt: "📈 Análise de Tendência de Crescimento"
    },
    "metrics.lastDays": {
        en: "Last {0} days",
        pt: "Últimos {0} dias"
    },
    "metrics.avgMonthlyGrowth": {
        en: "Avg Monthly Growth",
        pt: "Crescimento Médio Mensal"
    },
    "metrics.monthsToQuota": {
        en: "Months to Quota",
        pt: "Meses até Quota"
    },
    "metrics.estimatedDate": {
        en: "Estimated Date",
        pt: "Data Estimada"
    },
    "metrics.projectedCost": {
        en: "Projected Cost (12 months)",
        pt: "Custo Projetado (12 meses)"
    },
    "metrics.tenantStorage": {
        en: "🏢 SharePoint Tenant Storage",
        pt: "🏢 Storage do Tenant SharePoint"
    },
    "metrics.simulationMode": {
        en: "🧪 Simulation Mode",
        pt: "🧪 Modo Simulação"
    },
    "metrics.realData": {
        en: "REAL DATA",
        pt: "DADOS REAIS"
    },
    "metrics.simulationData": {
        en: "SIMULATION",
        pt: "SIMULAÇÃO"
    },
    "metrics.monthlyGrowthGB": {
        en: "Monthly Growth (GB)",
        pt: "Crescimento Mensal (GB)"
    },
    "metrics.currentTotalStorageGB": {
        en: "Current Total Storage (GB)",
        pt: "Storage Total Atual (GB)"
    },
    "metrics.monthlyGrowthLabel": {
        en: "Monthly Growth:",
        pt: "Crescimento Mensal:"
    },
    "metrics.currentStorageLabel": {
        en: "Current Total Storage:",
        pt: "Storage Total Atual:"
    },
    "metrics.gbMonth": {
        en: "GB/month",
        pt: "GB/mês"
    },
    "metrics.recalculate": {
        en: "🔄 Recalculate",
        pt: "🔄 Recalcular"
    },
    "metrics.avgGrowthMonth": {
        en: "Avg Growth/Month",
        pt: "Crescimento Médio/Mês"
    },
    "metrics.monthsUntilQuota": {
        en: "Months Until Quota",
        pt: "Meses até Quota"
    },
    "metrics.extraCost12Months": {
        en: "Extra Cost (12 months)",
        pt: "Custo Extra (12 meses)"
    },
    "metrics.consumptionHistory": {
        en: "📉 Consumption History",
        pt: "📉 Histórico de Consumo"
    },
    "metrics.startFrom": {
        en: "Start from:",
        pt: "Iniciar em:"
    },
    "metrics.growthProjection": {
        en: "🔮 Growth and Cost Projection (Next 12 months)",
        pt: "🔮 Projeção de Crescimento e Custo (Próximos 12 meses)"
    },
    "metrics.completeView": {
        en: "📊 Complete View: History (180 days) + Projection (12 months)",
        pt: "📊 Visão Completa: Histórico (180 dias) + Projeção (12 meses)"
    },
    "metrics.detailedProjection": {
        en: "📋 Detailed Projection",
        pt: "📋 Projeção Detalhada"
    },
    "metrics.table.month": {
        en: "Month",
        pt: "Mês"
    },
    "metrics.table.projectedStorage": {
        en: "Projected Storage",
        pt: "Storage Projetado"
    },
    "metrics.table.quotaPercent": {
        en: "% of Quota",
        pt: "% da Quota"
    },
    "metrics.table.excess": {
        en: "Excess",
        pt: "Excedente"
    },
    "metrics.table.monthlyCost": {
        en: "Monthly Cost",
        pt: "Custo Mensal"
    },
    "metrics.table.cumulativeCost": {
        en: "Cumulative Cost",
        pt: "Custo Acumulado"
    },
    "metrics.stable": {
        en: "Stable",
        pt: "Estável"
    },
    "metrics.alreadyExceeded": {
        en: "Already exceeded!",
        pt: "Já excedido!"
    },
    "metrics.months": {
        en: "months",
        pt: "meses"
    },
    "metrics.period": {
        en: "Period: {0} ({1} to {2})",
        pt: "Período: {0} ({1} a {2})"
    },
    "metrics.noHistoryFound": {
        en: "No history found for this site.",
        pt: "Nenhum histórico encontrado para este site."
    },
    "metrics.chart.storageTotal": {
        en: "Storage Total (GB)",
        pt: "Storage Total (GB)"
    },
    "metrics.chart.growth": {
        en: "Growth (GB)",
        pt: "Crescimento (GB)"
    },
    "metrics.chart.projectedStorage": {
        en: "Projected Storage (GB)",
        pt: "Storage Projetado (GB)"
    },
    "metrics.chart.tenantQuota": {
        en: "Tenant Quota (GB)",
        pt: "Quota do Tenant (GB)"
    },
    "metrics.chart.cumulativeCost": {
        en: "Cumulative Cost",
        pt: "Custo Acumulado"
    },
    "metrics.chart.historicalStorage": {
        en: "Historical Storage (GB)",
        pt: "Storage Histórico (GB)"
    },
    "metrics.chart.storageGB": {
        en: "Storage (GB)",
        pt: "Storage (GB)"
    },
    "metrics.chart.cost": {
        en: "Cost",
        pt: "Custo"
    },
    "metrics.chart.historical": {
        en: "Historical (GB)",
        pt: "Histórico (GB)"
    },
    "metrics.chart.projection": {
        en: "Projection (GB)",
        pt: "Projeção (GB)"
    },
    "metrics.now": {
        en: "Now!",
        pt: "Agora!"
    },
    "archive.hotCostPerGB": {
        en: "${0}/GB/month",
        pt: "${0}/GB/mês"
    },
    "archive.archiveCostPerGB": {
        en: "${0}/GB/month ({1}% off)",
        pt: "${0}/GB/mês ({1}% off)"
    },
    "archive.noInactiveSites": {
        en: "No inactive sites found for the period of {0} days",
        pt: "Nenhum site inativo encontrado para o período de {0} dias"
    },
    "archive.tryLargerPeriod": {
        en: "Try selecting a larger period",
        pt: "Tente selecionar um período maior"
    },
    "sites.clickForHistory": {
        en: "Click to view processing history",
        pt: "Clique para ver histórico de processamento"
    },
    "excluded.siteProtected": {
        en: "This site is in the exclusion list and will not be processed",
        pt: "Este site está na lista de exclusão e não será processado"
    },
    "badge.excluded": {
        en: "Excluded",
        pt: "Excluído"
    },
    "badge.target": {
        en: "Target",
        pt: "Alvo"
    },
    "sites.processingTarget": {
        en: "This site is selected for processing",
        pt: "Este site está selecionado para processamento"
    },
    "settings.archiveCostPerGB": {
        en: "Archive cost per GB/month (USD)",
        pt: "Custo Archive por GB/mês (USD)"
    },
    "settings.archiveCostPerGBDesc": {
        en: "Calculated automatically based on discount",
        pt: "Calculado automaticamente baseado no desconto"
    },
    "settings.archiveCostPreview": {
        en: "📦 Archive Cost Preview",
        pt: "📦 Pré-visualização de Custos Archive"
    },
    "settings.hotStorage1TBYear": {
        en: "Hot Storage (1 TB/year):",
        pt: "Hot Storage (1 TB/ano):"
    },
    "settings.archive1TBYear": {
        en: "Archive (1 TB/year):",
        pt: "Archive (1 TB/ano):"
    },
    "settings.savings1TBYear": {
        en: "Savings (1 TB/year):",
        pt: "Economia (1 TB/ano):"
    },
    "settings.refreshIntervalLabel": {
        en: "Refresh Interval (seconds)",
        pt: "Intervalo de Refresh (segundos)"
    },
    "settings.refreshIntervalLabelDesc": {
        en: "Frequency of data updates",
        pt: "Frequência de atualização dos dados"
    },
    "settings.rootDirectory": {
        en: "Root Directory",
        pt: "Diretório Raiz"
    },
    "settings.rootDirectoryDesc": {
        en: "Base folder where the application is installed",
        pt: "Pasta base onde a aplicação está instalada"
    },
    "settings.applicationFolder": {
        en: "Application Folder",
        pt: "Pasta da Aplicação"
    },
    "settings.applicationFolderDesc": {
        en: "Application folder name within the root directory",
        pt: "Nome da pasta da aplicação dentro do diretório raiz"
    },
    "settings.logsSubfolder": {
        en: "Logs Subfolder",
        pt: "Subpasta de Logs"
    },
    "settings.logsSubfolderDesc": {
        en: "Relative folder for log and data files",
        pt: "Pasta relativa para arquivos de log e dados"
    },
    "settings.fullPaths": {
        en: "📂 Calculated Full Paths",
        pt: "📂 Caminhos Completos Calculados"
    },
    "settings.restoreDefaults": {
        en: "🔄 Restore Defaults",
        pt: "🔄 Restaurar Padrões"
    },
    "settings.saveSettings": {
        en: "✅ Save Settings",
        pt: "✅ Salvar Configurações"
    },
    "modal.addExcludedSite": {
        en: "➕ Add Excluded Site",
        pt: "➕ Adicionar Site Excluído"
    },
    "modal.editExcludedSite": {
        en: "✏️ Edit Excluded Site",
        pt: "✏️ Editar Site Excluído"
    },
    "modal.siteName": {
        en: "Site Name *",
        pt: "Nome do Site *"
    },
    "modal.siteUrl": {
        en: "Site URL *",
        pt: "URL do Site *"
    },
    "modal.exclusionReason": {
        en: "Exclusion Reason *",
        pt: "Motivo da Exclusão *"
    },
    "modal.siteNamePlaceholder": {
        en: "Ex: Compliance",
        pt: "Ex: Compliance"
    },
    "modal.siteUrlPlaceholder": {
        en: "https://contoso.sharepoint.com/sites/...",
        pt: "https://contoso.sharepoint.com/sites/..."
    },
    "modal.reasonPlaceholder": {
        en: "Ex: Legal requirement - mandatory retention",
        pt: "Ex: Requisito legal - retenção obrigatória"
    },
    "modal.confirmDelete": {
        en: "🗑️ Confirm Deletion",
        pt: "🗑️ Confirmar Exclusão"
    },
    "modal.confirmDeleteText": {
        en: "Are you sure you want to remove",
        pt: "Tem certeza que deseja remover"
    },
    "modal.fromExclusionList": {
        en: "from the exclusion list?",
        pt: "da lista de exclusão?"
    },
    "modal.deleteWarning": {
        en: "⚠️ This site will be processed in the next executions.",
        pt: "⚠️ Este site voltará a ser processado nas próximas execuções."
    },
    "modal.remove": {
        en: "Remove",
        pt: "Remover"
    },
    "confirm.restoreDefaults": {
        en: "Are you sure you want to restore default settings?",
        pt: "Tem certeza que deseja restaurar as configurações padrão?"
    },
    "confirm.addToExclusion": {
        en: "Do you want to add {0} site(s) to the exclusion list?\\n\\nThis will prevent these sites from having their versions deleted.",
        pt: "Deseja adicionar {0} site(s) à lista de exclusão?\\n\\nIsso impedirá que estes sites tenham suas versões deletadas."
    },
    "settings.backupSubfolder": {
        en: "Backup Subfolder",
        pt: "Subpasta de Backup"
    },
    "settings.backupSubfolderDesc": {
        en: "Relative folder for file backup",
        pt: "Pasta relativa para backup de arquivos"
    },
    "settings.exportConfig": {
        en: "💾 Export Config",
        pt: "💾 Exportar Config"
    },
    "settings.exportAppPaths": {
        en: "📁 Export AppPaths",
        pt: "📁 Exportar AppPaths"
    },
    "settings.saved": {
        en: "Settings saved successfully!",
        pt: "Configurações salvas com sucesso!"
    },
    "settings.restored": {
        en: "Settings restored!",
        pt: "Configurações restauradas!"
    },
    "settings.storageCostsDesc": {
        en: "Base cost for SharePoint Online Extra File Storage. Changing any field auto-recalculates all others and all dashboard tabs.",
        pt: "Custo base do Extra File Storage do SharePoint Online. Alterar qualquer campo recalcula automaticamente os demais e todas as abas do dashboard."
    },
    "settings.costPerGBMonthUSD": {
        en: "Cost per GB/month (USD)",
        pt: "Custo por GB/mês (USD)"
    },
    "settings.costPerGBMonthUSDDesc": {
        en: "Microsoft SPO Extra File Storage list price in US Dollars",
        pt: "Preço de tabela do Extra File Storage da Microsoft em Dólares"
    },
    "settings.costPerGBMonthBRL": {
        en: "Cost per GB/month (BRL)",
        pt: "Custo por GB/mês (BRL)"
    },
    "settings.costPerGBMonthBRLDesc": {
        en: "Auto-calculated (USD × rate), or type to set manually",
        pt: "Calculado automaticamente (USD × taxa), ou digite para definir manualmente"
    },

    // ==================== SITE HISTORY POPUP ====================
    "history.title": {
        en: "Site Processing History",
        pt: "Histórico de Processamento do Site"
    },
    "history.storageOverview": {
        en: "Storage Overview",
        pt: "Visão Geral de Storage"
    },
    "history.firstExecSize": {
        en: "Size at 1st Execution",
        pt: "Tamanho na 1ª Execução"
    },
    "history.currentSize": {
        en: "Current Size",
        pt: "Tamanho Atual"
    },
    "history.currentVersions": {
        en: "Current Versions",
        pt: "Versões Atuais"
    },
    "history.versionPercent": {
        en: "% Versions of Total",
        pt: "% Versões no Total"
    },
    "history.trend.warning": {
        en: "Versions growing above average",
        pt: "Versões crescendo acima da média"
    },
    "history.trend.good": {
        en: "Versions stabilizing",
        pt: "Versões estabilizando"
    },
    "history.trend.clean": {
        en: "Site clean",
        pt: "Site limpo"
    },
    "history.trend.normal": {
        en: "Normal pattern",
        pt: "Padrão normal"
    },
    "history.totalExecutions": {
        en: "Total Executions",
        pt: "Total de Execuções"
    },
    "history.firstExecution": {
        en: "First Execution",
        pt: "Primeira Execução"
    },
    "history.lastExecution": {
        en: "Last Execution",
        pt: "Última Execução"
    },
    "history.totalDeleted": {
        en: "Total Versions Deleted",
        pt: "Total Versões Deletadas"
    },
    "history.totalFreed": {
        en: "Total Space Freed",
        pt: "Total Espaço Liberado"
    },
    "history.executionHistory": {
        en: "Execution History",
        pt: "Histórico de Execuções"
    },
    "history.column.dateTime": {
        en: "Date/Time",
        pt: "Data/Hora"
    },
    "history.column.type": {
        en: "Type",
        pt: "Tipo"
    },
    "history.column.status": {
        en: "Status",
        pt: "Status"
    },
    "history.column.sizeBefore": {
        en: "Size Before",
        pt: "Tamanho Antes"
    },
    "history.column.files": {
        en: "Files",
        pt: "Arquivos"
    },
    "history.column.versionsProc": {
        en: "Versions Proc.",
        pt: "Versões Proc."
    },
    "history.column.deleted": {
        en: "Deleted",
        pt: "Deletadas"
    },
    "history.column.freed": {
        en: "Freed",
        pt: "Liberado"
    },
    "history.column.cumulative": {
        en: "Cumulative",
        pt: "Acumulado"
    },
    "history.column.duration": {
        en: "Duration",
        pt: "Duração"
    },
    "history.hollowSuccess.label": {
        en: "No Effect",
        pt: "Sem Efeito"
    },
    "history.hollowSuccess.tooltip": {
        en: "Job completed but 0 versions deleted - retention hold likely still active",
        pt: "Job concluído mas 0 versões excluídas - retenção provavelmente ainda ativa"
    },
    "history.evolutionChart": {
        en: "Space Freed Evolution per Execution",
        pt: "Evolução do Espaço Liberado por Execução"
    },
    "history.siteTimelineChart": {
        en: "Site Storage & Cost Timeline",
        pt: "Linha do Tempo de Storage e Custo do Site"
    },
    "history.timeline.siteSize": {
        en: "Site Size",
        pt: "Tamanho do Site"
    },
    "history.timeline.cumulativeFreed": {
        en: "Cumulative Freed",
        pt: "Liberado Acumulado"
    },
    "history.timeline.monthlyCost": {
        en: "Monthly Cost",
        pt: "Custo Mensal"
    },
    "history.timeline.ofTenant": {
        en: "of tenant",
        pt: "do tenant"
    },
    "history.timeline.ofTenantCost": {
        en: "of tenant cost",
        pt: "do custo do tenant"
    },
    "history.timeline.sizeAxis": {
        en: "Size",
        pt: "Tamanho"
    },
    "history.timeline.costAxis": {
        en: "Cost",
        pt: "Custo"
    },
    "history.timeline.versionSize": {
        en: "Version Size",
        pt: "Tamanho das Versões"
    },
    "history.timeline.versionsKept": {
        en: "versions kept",
        pt: "versões mantidas"
    },
    "history.versionImpact.title": {
        en: "Version Retention Impact",
        pt: "Impacto da Retenção de Versões"
    },
    "history.versionImpact.limit": {
        en: "Version Limit",
        pt: "Limite de Versões"
    },
    "history.versionImpact.kept": {
        en: "Versions Kept (last)",
        pt: "Versões Mantidas (última)"
    },
    "history.versionImpact.versionsBefore": {
        en: "Versions Before",
        pt: "Versões Antes"
    },
    "history.versionImpact.versionsAfter": {
        en: "Versions After",
        pt: "Versões Depois"
    },
    "history.versionImpact.costStillUsed": {
        en: "Version storage cost",
        pt: "Custo do storage em versões"
    },
    "history.versionImpact.noVersionCost": {
        en: "No version storage remaining — site is clean",
        pt: "Nenhum storage em versões restante — site limpo"
    },
    "history.close": {
        en: "Close",
        pt: "Fechar"
    },

    // ==================== RETENTION POLICY ====================
    "sites.column.retention": {
        en: "Retention",
        pt: "Retenção"
    },
    "retention.suspended": {
        en: "Suspended",
        pt: "Suspensa"
    },
    "retention.currentlySuspended": {
        en: "Retention policy currently suspended for this site",
        pt: "Política de retenção atualmente suspensa para este site"
    },
    "retention.overviewTitle": {
        en: "Retention Policy Management",
        pt: "Gestão de Política de Retenção"
    },
    "retention.cyclesManaged": {
        en: "Cycles Managed",
        pt: "Ciclos Gerenciados"
    },
    "retention.avgWait": {
        en: "Avg Wait",
        pt: "Espera Média"
    },
    "retention.totalWait": {
        en: "Total Wait",
        pt: "Espera Total"
    },
    "retention.freedWithRetention": {
        en: "Freed (w/ Retention)",
        pt: "Liberado (c/ Retenção)"
    },
    "retention.policiesInvolved": {
        en: "Policies involved",
        pt: "Políticas envolvidas"
    },
    "retention.impactPositive": {
        en: "Retention bypass improved avg cleanup",
        pt: "Bypass de retenção melhorou limpeza média"
    },
    "retention.perExecution": {
        en: "per execution",
        pt: "por execução"
    },
    "nav.retention": {
        en: "Retention",
        pt: "Retenção"
    },
    "retention.tab.title": {
        en: "Retention Policy Management",
        pt: "Gestão de Política de Retenção"
    },
    "retention.tab.refresh": {
        en: "Refresh",
        pt: "Atualizar"
    },
    "retention.tab.totalPolicies": {
        en: "Total Policies",
        pt: "Total de Políticas"
    },
    "retention.tab.currentExceptions": {
        en: "Current Exceptions",
        pt: "Exceções Atuais"
    },
    "retention.tab.suspendedByUs": {
        en: "Suspended By Us",
        pt: "Suspensas por Nós"
    },
    "retention.tab.capacityAvailable": {
        en: "Capacity Available",
        pt: "Capacidade Disponível"
    },
    "retention.tab.noData": {
        en: "No retention policy data available. Run the orchestration with -ManageRetentionPolicy to generate data.",
        pt: "Nenhum dado de política de retenção disponível. Execute a orquestração com -ManageRetentionPolicy para gerar dados."
    },
    "retention.tab.allSites": {
        en: "All Sites",
        pt: "Todos os Sites"
    },
    "retention.tab.explicitSites": {
        en: "Explicit Sites",
        pt: "Sites Explícitos"
    },
    "retention.tab.enabled": {
        en: "Enabled",
        pt: "Habilitada"
    },
    "retention.tab.disabled": {
        en: "Disabled",
        pt: "Desabilitada"
    },
    "retention.tab.exceptionCapacity": {
        en: "Exception Capacity",
        pt: "Capacidade de Exceções"
    },
    "retention.tab.suspendedByUsLabel": {
        en: "Suspended by SPO Version Management",
        pt: "Suspensas pelo SPO Version Management"
    },
    "retention.tab.otherExceptions": {
        en: "Other exceptions",
        pt: "Outras exceções"
    },
    "retention.tab.lastUpdated": {
        en: "Last updated",
        pt: "Última atualização"
    },
    "retention.tab.clickForDetails": {
        en: "Click to show/hide policy details",
        pt: "Clique para mostrar/ocultar detalhes da política"
    },
    "retention.tab.mode": {
        en: "Mode",
        pt: "Modo"
    },
    "retention.tab.created": {
        en: "Created",
        pt: "Criada"
    },
    "retention.tab.modified": {
        en: "Modified",
        pt: "Modificada"
    },
    "retention.tab.inclusionType": {
        en: "Inclusion Type",
        pt: "Tipo de Inclusão"
    },
    "retention.tab.includedSites": {
        en: "Included Sites",
        pt: "Sites Incluídos"
    },
    "retention.tab.exceptionSites": {
        en: "Exception Sites",
        pt: "Sites em Exceção"
    },
    "retention.tab.siteUrl": {
        en: "Site URL",
        pt: "URL do Site"
    },
    "retention.tab.source": {
        en: "Source",
        pt: "Origem"
    },
    "retention.tab.suspendedAt": {
        en: "Suspended At",
        pt: "Suspenso Em"
    },
    "history.column.retention": {
        en: "Retention",
        pt: "Retenção"
    },

    // ==================== PROGRESS / STATUS ====================
    "progress.completed": {
        en: "Completed! {0} jobs processed",
        pt: "Concluído! {0} jobs processados"
    },
    "progress.processing": {
        en: "Processing: {0} of {1} jobs completed",
        pt: "Processando: {0} de {1} jobs concluídos"
    },
    "progress.waiting": {
        en: "Waiting to start...",
        pt: "Aguardando início..."
    },

    // ==================== PHASE PROGRESS BAR ====================
    "phase.queued": {
        en: "Queued",
        pt: "Na Fila"
    },
    "phase.sync": {
        en: "Sync Policy",
        pt: "Sincronizar"
    },
    "phase.retention": {
        en: "Retention",
        pt: "Retenção"
    },
    "phase.delete": {
        en: "BatchDelete",
        pt: "Exclusão"
    },
    "phase.complete": {
        en: "Complete",
        pt: "Concluído"
    },
    "phase.skipped": {
        en: "Skipped",
        pt: "Ignorado"
    },

    // ==================== JOBS CARDS / DETAILS ====================
    "jobs.empty.active": {
        en: "No jobs running",
        pt: "Nenhum job em execução"
    },
    "jobs.empty.queue": {
        en: "No sites in queue",
        pt: "Nenhum site na fila"
    },
    "jobs.empty.completed": {
        en: "No completed jobs yet",
        pt: "Nenhum job concluído ainda"
    },
    "jobs.syncPolicy": {
        en: "SyncListPolicy",
        pt: "SyncListPolicy"
    },
    "jobs.batchDelete": {
        en: "BatchDelete",
        pt: "BatchDelete"
    },
    "jobs.waiting": {
        en: "Waiting...",
        pt: "Aguardando..."
    },
    "jobs.duration": {
        en: "Duration",
        pt: "Duração"
    },
    "jobs.lists": {
        en: "Lists",
        pt: "Listas"
    },
    "jobs.files": {
        en: "Files",
        pt: "Arquivos"
    },
    "jobs.synced": {
        en: "Synced",
        pt: "Sincronizadas"
    },
    "jobs.minTotal": {
        en: "Min Total",
        pt: "Min Total"
    },
    "jobs.versionsDeleted": {
        en: "Versions Del.",
        pt: "Versões Del."
    },
    "jobs.released": {
        en: "Released",
        pt: "Liberado"
    },
    "jobs.versionsProc": {
        en: "Versions: {0} proc, {1} del.",
        pt: "Versões: {0} proc, {1} del."
    },
    "jobs.success": {
        en: "SUCCESS",
        pt: "SUCESSO"
    },
    "jobs.error": {
        en: "ERROR",
        pt: "ERRO"
    },
    "jobs.partial": {
        en: "PARTIAL",
        pt: "PARCIAL"
    },

    // ==================== STORAGE LABELS ====================
    "storage.before": {
        en: "Before",
        pt: "Antes"
    },
    "storage.released": {
        en: "Released",
        pt: "Liberado"
    },
    "storage.economy": {
        en: "Savings",
        pt: "Economia"
    },
    "storage.releasedSession": {
        en: "Released (Session)",
        pt: "Liberado (Sessão)"
    },
    "storage.percentReleased": {
        en: "% Released",
        pt: "% Liberado"
    },
    "storage.available": {
        en: "Available",
        pt: "Disponível"
    },
    "storage.totalSites": {
        en: "Total Sites",
        pt: "Total Sites"
    },
    "storage.totalVersions": {
        en: "Total Versions",
        pt: "Total Versões"
    },
    "storage.versionSize": {
        en: "Version Size",
        pt: "Tamanho Versões"
    },
    "storage.totalFreedHistory": {
        en: "Total Freed (History)",
        pt: "Total Liberado (Histórico)"
    },
    "metrics.projected.title": {
        en: "Projected Storage After Version Cleanup",
        pt: "Projeção de Storage Após Limpeza de Versões"
    },
    "metrics.projected.note": {
        en: "⚠️ Projected — updates after next Get-SPOSites run",
        pt: "⚠️ Projetado — atualiza após próxima execução de Get-SPOSites"
    },
    "metrics.projected.current": {
        en: "Current Usage",
        pt: "Uso Atual"
    },
    "metrics.projected.afterCleanup": {
        en: "After Version Cleanup (Projected)",
        pt: "Após Limpeza de Versões (Projetado)"
    },
    "metrics.projected.freedSpace": {
        en: "Freed Space (Versions)",
        pt: "Espaço Liberado (Versões)"
    },
    "metrics.projected.newUsage": {
        en: "New Usage %",
        pt: "Novo Uso %"
    },
    "metrics.projected.extraMonths": {
        en: "Extra Months Before Quota",
        pt: "Meses Extras Antes da Quota"
    },
    "storage.tenantQuota": {
        en: "Tenant Quota",
        pt: "Quota do Tenant"
    },
    "storage.storageUsed": {
        en: "Storage Used",
        pt: "Storage Usado"
    },
    "simulation.mode": {
        en: "🧪 Simulation Mode",
        pt: "🧪 Modo Simulação"
    },
    "simulation.badge": {
        en: "SIMULATION",
        pt: "SIMULAÇÃO"
    },
    "simulation.titleGrowth": {
        en: "Simulate Growth",
        pt: "Simular Crescimento"
    },
    "simulation.growthRate": {
        en: "Growth Rate (GB/month)",
        pt: "Taxa de Crescimento (GB/mês)"
    },
    "simulation.totalStorage": {
        en: "Total Storage (GB)",
        pt: "Storage Total (GB)"
    },
    "simulation.byGrowth": {
        en: "By growth rate",
        pt: "Por taxa de crescimento"
    },
    "simulation.byTotal": {
        en: "By total storage",
        pt: "Por storage total"
    },
    "cost.extraEstimated": {
        en: "EXTRA COST ESTIMATED",
        pt: "CUSTO EXTRA ESTIMADO"
    },
    "cost.exceededStorage": {
        en: "Exceeded Storage:",
        pt: "Storage Excedente:"
    },
    "cost.annualEstimate": {
        en: "Estimated Annual Cost:",
        pt: "Custo Anual Estimado:"
    },
    "cost.basedOn": {
        en: "Based on: {0} per TB/year",
        pt: "Base: {0} por TB/ano"
    },
    "cost.perTBYear": {
        en: "per TB/year",
        pt: "por TB/ano"
    },
    "cost.extraStorageExplain": {
        en: "This is the annual cost of storage exceeding your tenant quota, based on the Extra File Storage price configured in Settings.",
        pt: "Este é o custo anual do storage que excede a cota do seu tenant, baseado no preço do Extra File Storage configurado no Settings."
    },

    // ==================== SITE DETAILS ====================
    "siteDetail.versions": {
        en: "Versions",
        pt: "Versões"
    },
    "siteDetail.versionSize": {
        en: "Version Size",
        pt: "Tamanho Versões"
    },
    "siteDetail.policy": {
        en: "Policy",
        pt: "Política"
    },
    "siteDetail.tenant": {
        en: "Tenant",
        pt: "Tenant"
    },
    "siteDetail.status": {
        en: "Status",
        pt: "Status"
    },
    "siteDetail.active": {
        en: "Active",
        pt: "Ativo"
    },
    "siteDetail.archived": {
        en: "Archived",
        pt: "Arquivado"
    },
    "siteDetail.lock": {
        en: "Lock",
        pt: "Lock"
    },
    "siteDetail.subsites": {
        en: "Subsites",
        pt: "Subsites"
    },

    // ==================== BADGE TEXTS ====================
    "badge.syncPending": {
        en: "SYNC PENDING",
        pt: "SYNC PENDENTE"
    },
    "badge.syncDone": {
        en: "SYNC ✓",
        pt: "SYNC ✓"
    },
    "badge.syncRunning": {
        en: "SYNC Running",
        pt: "SYNC Em Execução"
    },
    "badge.syncError": {
        en: "SYNC Error",
        pt: "SYNC Erro"
    },
    "badge.deletePending": {
        en: "DELETE PENDING",
        pt: "DELETE PENDENTE"
    },
    "badge.deleteDone": {
        en: "DELETE ✓",
        pt: "DELETE ✓"
    },
    "badge.deleteError": {
        en: "DELETE Error",
        pt: "DELETE Erro"
    },
    "badge.deleteSkipped": {
        en: "No versions to delete",
        pt: "Sem versões para deletar"
    },
    "status.complete": {
        en: "COMPLETE",
        pt: "COMPLETO"
    },
    "status.syncOnlyTitle": {
        en: "Sync Only - No versions to delete",
        pt: "Apenas Sync - Sem versões para deletar"
    },
    "badge.complete": {
        en: "COMPLETE",
        pt: "COMPLETO"
    },
    "badge.success": {
        en: "SUCCESS",
        pt: "SUCESSO"
    },
    "badge.partial": {
        en: "PARTIAL",
        pt: "PARCIAL"
    },
    "badge.pending": {
        en: "PENDING",
        pt: "PENDENTE"
    },

    // ==================== QUEUE SECTION ====================
    "queue.title": {
        en: "Queue - Sync",
        pt: "Fila - Sync"
    },
    "queue.sync": {
        en: "Sync",
        pt: "Sync"
    },
    "queue.delete": {
        en: "Delete",
        pt: "Delete"
    },
    "queue.all": {
        en: "All",
        pt: "Todos"
    },
    "queue.waitingSlot": {
        en: "Waiting slot for",
        pt: "Aguardando slot para"
    },
    "queue.versions": {
        en: "versions",
        pt: "versões"
    },
    "queue.moreInQueue": {
        en: "... and {0} more sites in queue",
        pt: "... e mais {0} sites na fila"
    },
    "queue.loadMore": {
        en: "+10 more",
        pt: "+10 mais"
    },
    "queue.showAll": {
        en: "Show all ({0})",
        pt: "Mostrar todos ({0})"
    },
    "queue.showingAll": {
        en: "Showing all {0} sites",
        pt: "Mostrando todos os {0} sites"
    },
    "queue.collapse": {
        en: "Collapse list",
        pt: "Recolher lista"
    },
    "queue.noSitesFilter": {
        en: "No sites in {0} queue",
        pt: "Nenhum site na fila {0}"
    },

    // ==================== COMPLETED SECTION ====================
    "completed.recent": {
        en: "Recently Completed",
        pt: "Últimos Concluídos"
    },
    "completed.filterPlaceholder": {
        en: "Filter by URL...",
        pt: "Filtrar por URL..."
    },
    "completed.sortBy": {
        en: "Sort by:",
        pt: "Ordenar por:"
    },
    "completed.sortRecent": {
        en: "Most Recent",
        pt: "Mais Recente"
    },
    "completed.sortEconomy": {
        en: "Highest Savings (%)",
        pt: "Maior Economia (%)"
    },
    "completed.sortReleased": {
        en: "Most Storage Freed",
        pt: "Maior Storage Liberado"
    },
    "completed.sortSize": {
        en: "Largest Size",
        pt: "Maior Espaço Ocupado"
    },
    "completed.sortName": {
        en: "Name (A-Z)",
        pt: "Nome (A-Z)"
    },
    "completed.statusFilter": {
        en: "Status:",
        pt: "Status:"
    },
    "completed.statusAll": {
        en: "All",
        pt: "Todos"
    },
    "completed.statusComplete": {
        en: "Complete",
        pt: "Completos"
    },
    "completed.statusSyncOnly": {
        en: "Sync Only",
        pt: "Apenas Sync"
    },
    "completed.statusPartial": {
        en: "Partial",
        pt: "Parciais"
    },
    "completed.statusError": {
        en: "With Error",
        pt: "Com Erro"
    },
    "completed.showing": {
        en: "Showing {0}-{1} of {2}",
        pt: "Mostrando {0}-{1} de {2}"
    },
    "completed.prev": {
        en: "◀ Previous",
        pt: "◀ Anterior"
    },
    "completed.next": {
        en: "Next ▶",
        pt: "Próximo ▶"
    },
    "completed.perPage": {
        en: "Per page:",
        pt: "Por página:"
    },

    // ==================== COMMON / BUTTONS ====================
    "common.loading": {
        en: "Loading...",
        pt: "Carregando..."
    },
    "common.lastUpdate": {
        en: "Last update",
        pt: "Última atualização"
    },
    "common.refresh": {
        en: "Refresh",
        pt: "Refresh"
    },
    "common.add": {
        en: "Add",
        pt: "Adicionar"
    },
    "common.update": {
        en: "Update",
        pt: "Atualizar"
    },
    "common.reason": {
        en: "Reason",
        pt: "Motivo"
    },
    "common.excludedAt": {
        en: "Excluded at",
        pt: "Excluído em"
    },
    "common.protected": {
        en: "PROTECTED",
        pt: "PROTEGIDO"
    },
    "excluded.protectedSitesCount": {
        en: "{0} protected sites",
        pt: "{0} sites protegidos"
    },
    "excluded.protectedSitesDesc": {
        en: "These sites will NOT have versions deleted. Versions will be kept intact to ensure compliance and security.",
        pt: "Estes sites NÃO terão versões deletadas. As versões serão mantidas intactas para garantir conformidade e segurança."
    },
    "excluded.defaultReason": {
        en: "Exclusion list",
        pt: "Lista de exclusão"
    },
    "common.edit": {
        en: "Edit",
        pt: "Editar"
    },
    "common.remove": {
        en: "Remove",
        pt: "Remover"
    },
    "common.updated": {
        en: "Updated",
        pt: "Atualizado"
    },
    "common.updating": {
        en: "Updating...",
        pt: "Atualizando..."
    },
    "common.refreshing": {
        en: "Refreshing...",
        pt: "Atualizando..."
    },
    "common.error": {
        en: "Error",
        pt: "Erro"
    },
    "common.success": {
        en: "Success",
        pt: "Sucesso"
    },
    "common.cancel": {
        en: "Cancel",
        pt: "Cancelar"
    },
    "common.confirm": {
        en: "Confirm",
        pt: "Confirmar"
    },
    "common.delete": {
        en: "Delete",
        pt: "Deletar"
    },
    "common.save": {
        en: "Save",
        pt: "Salvar"
    },
    "common.close": {
        en: "Close",
        pt: "Fechar"
    },
    "common.yes": {
        en: "Yes",
        pt: "Sim"
    },
    "common.no": {
        en: "No",
        pt: "Não"
    },
    "common.na": {
        en: "N/A",
        pt: "N/D"
    },
    "common.minutes": {
        en: "min",
        pt: "min"
    },
    "common.month": {
        en: "month",
        pt: "mês"
    },
    "common.year": {
        en: "year",
        pt: "ano"
    },
    "common.days": {
        en: "days",
        pt: "dias"
    },
    "common.of": {
        en: "of",
        pt: "de"
    },
    "common.page": {
        en: "Page",
        pt: "Página"
    },
    "common.showing": {
        en: "Showing",
        pt: "Mostrando"
    },
    "common.to": {
        en: "to",
        pt: "a"
    },
    "common.selected": {
        en: "selected",
        pt: "selecionados"
    },

    // ==================== FOOTER ====================
    "footer.autoRefresh": {
        en: "Dashboard auto-refreshes every {0} seconds",
        pt: "Dashboard atualiza automaticamente a cada {0} segundos"
    },
    "footer.autoRefreshPrefix": {
        en: "Dashboard auto-refreshes every",
        pt: "Dashboard atualiza automaticamente a cada"
    },
    "footer.autoRefreshSuffix": {
        en: "seconds",
        pt: "segundos"
    },
    "footer.dataSource": {
        en: "Data source",
        pt: "Fonte de dados"
    },
    "footer.configStatus": {
        en: "Config",
        pt: "Config"
    },
    "footer.configLoaded": {
        en: "Loaded",
        pt: "Carregado"
    },
    "footer.configDefault": {
        en: "Default",
        pt: "Padrão"
    },

    // ==================== ADDITIONAL MESSAGES ====================
    "alerts.criticalStorageExceeded": {
        en: "CRITICAL WARNING: Tenant storage exceeded quota! The customer will be charged for the excess.",
        pt: "ATENÇÃO CRÍTICA: Storage do tenant excedeu a quota! O cliente será cobrado pelo excedente."
    },
    "alerts.criticalConsumption90": {
        en: "CRITICAL WARNING: Consumption above 90%! Imminent risk of extra cost. Free up space urgently!",
        pt: "AVISO CRÍTICO: Consumo acima de 90%! Risco iminente de custo extra. Libere espaço urgentemente!"
    },
    "alerts.warningConsumption80": {
        en: "WARNING: Consumption between 80-90%. Consider freeing up space to avoid extra costs.",
        pt: "ATENÇÃO: Consumo entre 80-90%. Considere liberar espaço para evitar custos extras."
    },
    "labels.versions": {
        en: "Versions:",
        pt: "Versões:"
    },
    "labels.versionSize": {
        en: "Version Size:",
        pt: "Tam. Versões:"
    },
    "empty.noExcludedSites": {
        en: "No sites in exclusion list",
        pt: "Nenhum site na lista de exclusão"
    },
    "empty.noCompletedJobs": {
        en: "No completed jobs yet",
        pt: "Nenhum job concluído ainda"
    },
    "empty.noMatchingResults": {
        en: "No matching results for the applied filters",
        pt: "Nenhum resultado encontrado para os filtros aplicados"
    },
    "empty.noSitesLoaded": {
        en: "No sites loaded",
        pt: "Nenhum site carregado"
    },
    "empty.noSitesMatchFilter": {
        en: "No sites match the filters",
        pt: "Nenhum site corresponde aos filtros"
    },
    "confirm.noSiteSelected": {
        en: "No sites selected",
        pt: "Nenhum site selecionado"
    },
    "confirm.addSitesToExclusion": {
        en: "Do you want to add {0} site(s) to the exclusion list?\\n\\nThis will prevent these sites from having their versions deleted.",
        pt: "Deseja adicionar {0} site(s) à lista de exclusão?\\n\\nIsso impedirá que estes sites tenham suas versões deletadas."
    },
    "confirm.addSitesToArchive": {
        en: "Do you want to add {0} site(s) to the archive queue?\\n\\nThese sites will be queued for archiving.",
        pt: "Deseja adicionar {0} site(s) à fila de arquivo?\\n\\nEstes sites serão enfileirados para arquivamento."
    },
    "confirm.clearArchiveQueue": {
        en: "Clear the archive queue? This only clears the dashboard view.",
        pt: "Limpar a fila de arquivo? Isso limpa apenas a visualização do dashboard."
    },
    "common.copied": {
        en: "Copied to clipboard!",
        pt: "Copiado para a área de transferência!"
    },
    "export.noSitesToExport": {
        en: "No sites to export",
        pt: "Nenhum site para exportar"
    },
    "currency.symbol": {
        en: "$",
        pt: "R$"
    },
    "currency.code": {
        en: "USD",
        pt: "BRL"
    },
    "common.waitingData": {
        en: "Waiting for data...",
        pt: "Aguardando dados..."
    },
    "common.waitingTenantData": {
        en: "Waiting for tenant data...",
        pt: "Aguardando dados do tenant..."
    },
    "common.loading": {
        en: "Loading...",
        pt: "Carregando..."
    },
    "sites.addedViaDashboard": {
        en: "Added via Dashboard - SharePoint Sites List",
        pt: "Adicionado via Dashboard - SharePoint Sites List"
    },
    "sites.fileNotFound": {
        en: "AllSites.json file not found. Run Export-AllSitesDataForDashboard in PowerShell.",
        pt: "Arquivo AllSites.json não encontrado. Execute Export-AllSitesDataForDashboard no PowerShell."
    },
    "error.loadingData": {
        en: "Error loading data:",
        pt: "Erro ao carregar dados:"
    },
    "error.loadingPrefix": {
        en: "Error loading:",
        pt: "Erro ao carregar:"
    },
    "excluded.inExclusionList": {
        en: "Site is in the exclusion list - cannot be selected",
        pt: "Site na lista de exclusão - não pode ser selecionado"
    },
    "pagination.showingOf": {
        en: "Showing {0}-{1} of {2}",
        pt: "Mostrando {0}-{1} de {2}"
    },

    // ==================== STATISTICS TAB ====================
    "nav.statistics": {
        en: "Statistics",
        pt: "Estatísticas"
    },
    "stats.title": {
        en: "Historical Statistics",
        pt: "Estatísticas Históricas"
    },
    "stats.sessionHistory": {
        en: "Session History",
        pt: "Histórico de Sessões"
    },
    "stats.storageEvolution": {
        en: "Storage Evolution Over Time",
        pt: "Evolução do Storage ao Longo do Tempo"
    },
    "stats.costAnalysis": {
        en: "Cost Analysis & Savings",
        pt: "Análise de Custos e Economia"
    },
    "stats.processedSites": {
        en: "Processed Sites Evolution",
        pt: "Evolução dos Sites Processados"
    },
    "stats.totalSessions": {
        en: "Total Sessions",
        pt: "Total de Sessões"
    },
    "stats.totalSpaceFreed": {
        en: "Total Space Freed",
        pt: "Total de Espaço Liberado"
    },
    "stats.totalVersionsDeleted": {
        en: "Total Versions Deleted",
        pt: "Total de Versões Excluídas"
    },
    "stats.totalSitesProcessed": {
        en: "Total Sites Processed",
        pt: "Total de Sites Processados"
    },
    "stats.annualSavings": {
        en: "Annualized Savings",
        pt: "Economia Anualizada"
    },
    "stats.annualSavingsUSD": {
        en: "Annual Savings (USD)",
        pt: "Economia Anual (USD)"
    },
    "stats.annualSavingsBRL": {
        en: "Annual Savings (BRL)",
        pt: "Economia Anual (BRL)"
    },
    "stats.costPerGB": {
        en: "Cost per GB/year",
        pt: "Custo por GB/ano"
    },
    "stats.costReduction": {
        en: "Cost Reduction Trend",
        pt: "Tendência de Redução de Custo"
    },
    "stats.savingsPerSession": {
        en: "Savings per Session",
        pt: "Economia por Sessão"
    },
    "stats.cumulativeSavings": {
        en: "Cumulative Savings",
        pt: "Economia Acumulada"
    },
    "stats.storageFreedPerSession": {
        en: "Storage Freed per Session",
        pt: "Espaço Liberado por Sessão"
    },
    "stats.sessionDate": {
        en: "Session Date",
        pt: "Data da Sessão"
    },
    "stats.sessionDuration": {
        en: "Duration",
        pt: "Duração"
    },
    "stats.sessionStatus": {
        en: "Status",
        pt: "Status"
    },
    "stats.spaceFreed": {
        en: "Space Freed",
        pt: "Espaço Liberado"
    },
    "stats.versionsDeleted": {
        en: "Versions Deleted",
        pt: "Versões Excluídas"
    },
    "stats.sitesProcessedCount": {
        en: "Sites Processed",
        pt: "Sites Processados"
    },
    "stats.noSessionData": {
        en: "No session data available yet. Run a processing session to see statistics.",
        pt: "Nenhum dado de sessão disponível ainda. Execute uma sessão de processamento para ver estatísticas."
    },
    "stats.exportPDF": {
        en: "Export as PDF",
        pt: "Exportar como PDF"
    },
    "stats.exportingPDF": {
        en: "Exporting...",
        pt: "Exportando..."
    },
    "stats.siteStorageTimeline": {
        en: "Site Storage Timeline",
        pt: "Linha do Tempo de Storage dos Sites"
    },
    "stats.selectSite": {
        en: "Select a site to view its storage evolution",
        pt: "Selecione um site para ver sua evolução de storage"
    },
    "stats.searchSitePlaceholder": {
        en: "🔍 Search by title or URL...",
        pt: "🔍 Buscar por título ou URL..."
    },
    "stats.showTop": {
        en: "Show Top",
        pt: "Exibir Top"
    },
    "stats.sitesShown": {
        en: "sites",
        pt: "sites"
    },
    "stats.gridTitle": {
        en: "Title",
        pt: "Título"
    },
    "stats.gridFreed": {
        en: "Space Freed",
        pt: "Espaço Liberado"
    },
    "stats.gridExecs": {
        en: "Executions",
        pt: "Execuções"
    },
    "stats.gridLastProc": {
        en: "Last Processed",
        pt: "Última Execução"
    },
    "stats.versionStorageCost": {
        en: "Version Storage Cost (USD/yr)",
        pt: "Custo de Storage de Versões (USD/ano)"
    },
    "stats.inVersions": {
        en: "in versions",
        pt: "em versões"
    },
    "stats.versionDataNotAvailable": {
        en: "Load AllSites.json for version data",
        pt: "Carregue AllSites.json para dados de versões"
    },
    "stats.storageBeforeAfter": {
        en: "Storage Before/After",
        pt: "Storage Antes/Depois"
    },
    "stats.trend": {
        en: "Trend",
        pt: "Tendência"
    },
    "stats.increasing": {
        en: "Increasing",
        pt: "Aumentando"
    },
    "stats.decreasing": {
        en: "Decreasing",
        pt: "Diminuindo"
    },
    "stats.stable": {
        en: "Stable",
        pt: "Estável"
    },
    "stats.annualNote": {
        en: "* Values annualized for impact analysis",
        pt: "* Valores anualizados para análise de impacto"
    },
    "stats.costBasis": {
        en: "Cost basis",
        pt: "Base de custo"
    },
    "stats.savingsImpact": {
        en: "Savings Impact",
        pt: "Impacto da Economia"
    },
    "stats.equivalentTo": {
        en: "Equivalent to",
        pt: "Equivalente a"
    },
    "stats.perYear": {
        en: "per year",
        pt: "por ano"
    },
    "stats.totalCleaned": {
        en: "Total Cleaned",
        pt: "Total Limpo"
    },

    // ==================== TENANT STORAGE TIMELINE ====================
    "stats.tenantTimeline": {
        en: "Tenant Storage Timeline",
        pt: "Linha do Tempo do Storage do Tenant"
    },
    "stats.tenantTimelineDesc": {
        en: "Actual tenant storage (Admin 365 / CSV) vs. cumulative space freed — history preserved across executions with financial savings projection",
        pt: "Storage real do tenant (Admin 365 / CSV) vs. espaço acumulado liberado — histórico preservado entre execuções com projeção de economia financeira"
    },
    "stats.actualStorage": {
        en: "Actual Tenant Storage",
        pt: "Storage Real do Tenant"
    },
    "stats.cumulativeCleaned": {
        en: "Cumulative Space Freed",
        pt: "Espaço Acumulado Liberado"
    },
    "stats.executionMarkers": {
        en: "Execution Sessions",
        pt: "Sessões de Execução"
    },
    "stats.timelineNoData": {
        en: "Timeline data will appear after the first execution with Graph API connected or CSV import (-GraphReportCSV).",
        pt: "Os dados da linha do tempo aparecerão após a primeira execução com a Graph API conectada ou importação CSV (-GraphReportCSV)."
    },
    "stats.timelineRange": {
        en: "Timeline spanning {days} days with {snapshots} execution snapshots",
        pt: "Linha do tempo abrangendo {days} dias com {snapshots} snapshots de execução"
    },
    "stats.timelineWindow": {
        en: "Data from Graph API or CSV import; accumulates across executions",
        pt: "Dados da Graph API ou importação CSV; acumulam entre execuções"
    },
    "stats.savingsTimeline": {
        en: "Cumulative Savings",
        pt: "Economia Acumulada"
    },
    "stats.annualSavingsUSDExplain": {
        en: "Storage freed × USD/GB/year cost from Settings",
        pt: "Storage liberado × custo USD/GB/ano do Settings"
    },
    "stats.annualSavingsBRLExplain": {
        en: "Storage freed × BRL/GB/year cost from Settings",
        pt: "Storage liberado × custo BRL/GB/ano do Settings"
    },
    "stats.totalCleanedExplain": {
        en: "Total file versions removed from all processed sites",
        pt: "Total de versões removidas de todos os sites processados"
    },
    "stats.versionStorageCostExplain": {
        en: "Annual cost of ALL existing file versions across the tenant",
        pt: "Custo anual de TODAS as versões de arquivos existentes no tenant"
    },
    "stats.extraStorageCost": {
        en: "Extra File Storage Cost (USD/yr)",
        pt: "Custo Extra de Storage (USD/ano)"
    },
    "stats.extraStorageCostExplain": {
        en: "Annual cost of storage exceeding the tenant quota (overage)",
        pt: "Custo anual do storage que excede a cota do tenant (excedente)"
    },
    "stats.overQuota": {
        en: "over quota",
        pt: "acima da cota"
    },
    "stats.withinQuota": {
        en: "Within quota - no extra charge",
        pt: "Dentro da cota - sem custo extra"
    },
    "stats.loadDataFirst": {
        en: "Load tenant data first",
        pt: "Carregue os dados do tenant primeiro"
    }
};

// Available languages
const AVAILABLE_LANGUAGES = [
    { code: 'en', name: 'English', flag: '🇺🇸' },
    { code: 'pt', name: 'Português', flag: '🇧🇷' }
];

// Current language (default: English)
let currentLanguage = 'en';

// Initialize language from localStorage or browser preference
function initializeLanguage() {
    // Try localStorage first
    const savedLang = localStorage.getItem('spo_dashboard_language');
    if (savedLang && AVAILABLE_LANGUAGES.some(l => l.code === savedLang)) {
        currentLanguage = savedLang;
    } else {
        // Try browser language
        const browserLang = navigator.language.substring(0, 2).toLowerCase();
        if (AVAILABLE_LANGUAGES.some(l => l.code === browserLang)) {
            currentLanguage = browserLang;
        }
    }
    
    // Set HTML lang attribute for CSS language selectors
    document.documentElement.lang = currentLanguage;
}

// Get translated string
function t(key, ...args) {
    const entry = LOCALIZATION[key];
    if (!entry) {
        console.warn(`Missing translation key: ${key}`);
        return key;
    }
    
    let text = entry[currentLanguage] || entry['en'] || key;
    
    // Replace placeholders {0}, {1}, etc.
    args.forEach((arg, index) => {
        text = text.replace(`{${index}}`, arg);
    });
    
    return text;
}

// Set language and update UI
function setLanguage(langCode) {
    if (!AVAILABLE_LANGUAGES.some(l => l.code === langCode)) {
        console.warn(`Unsupported language: ${langCode}`);
        return;
    }
    
    currentLanguage = langCode;
    localStorage.setItem('spo_dashboard_language', langCode);
    
    // Set HTML lang attribute for CSS language selectors
    document.documentElement.lang = langCode;
    
    // Update language selector display
    updateLanguageSelector();
    
    // Re-translate all elements with data-i18n attribute
    translatePage();
    
    // Update dynamic content
    if (typeof loadData === 'function') {
        loadData();
    }
}

// Get current language
function getCurrentLanguage() {
    return currentLanguage;
}

// Get language info
function getLanguageInfo(langCode) {
    return AVAILABLE_LANGUAGES.find(l => l.code === langCode) || AVAILABLE_LANGUAGES[0];
}

// Update language selector display
function updateLanguageSelector() {
    const langInfo = getLanguageInfo(currentLanguage);
    
    // Update header selector
    const headerFlag = document.getElementById('currentLangFlag');
    const headerName = document.getElementById('currentLangName');
    if (headerFlag) headerFlag.textContent = langInfo.flag;
    if (headerName) headerName.textContent = langInfo.name;
    
    // Update settings dropdown
    const settingsDropdown = document.getElementById('configLanguage');
    if (settingsDropdown) {
        settingsDropdown.value = currentLanguage;
    }
}

// Translate all elements with data-i18n attribute
function translatePage() {
    document.querySelectorAll('[data-i18n]').forEach(el => {
        const key = el.getAttribute('data-i18n');
        const args = el.getAttribute('data-i18n-args');
        const prefix = el.getAttribute('data-i18n-prefix') || '';
        
        let translatedText;
        if (args) {
            translatedText = t(key, ...JSON.parse(args));
        } else {
            translatedText = t(key);
        }
        el.textContent = prefix + translatedText;
    });
    
    // Translate elements with HTML content (data-i18n-html)
    document.querySelectorAll('[data-i18n-html]').forEach(el => {
        const key = el.getAttribute('data-i18n-html');
        const args = el.getAttribute('data-i18n-args');
        
        let translatedText;
        if (args) {
            translatedText = t(key, ...JSON.parse(args));
        } else {
            translatedText = t(key);
        }
        el.innerHTML = translatedText;
    });
    
    // Translate placeholders
    document.querySelectorAll('[data-i18n-placeholder]').forEach(el => {
        const key = el.getAttribute('data-i18n-placeholder');
        el.placeholder = t(key);
    });
    
    // Translate titles/tooltips
    document.querySelectorAll('[data-i18n-title]').forEach(el => {
        const key = el.getAttribute('data-i18n-title');
        el.title = t(key);
    });
}

// Render language selector HTML
function renderLanguageSelector() {
    const langInfo = getLanguageInfo(currentLanguage);
    
    return `
        <div class="language-selector" onclick="toggleLanguageDropdown(event)">
            <span id="currentLangFlag" class="lang-flag">${langInfo.flag}</span>
            <span id="currentLangName" class="lang-name">${langInfo.name}</span>
            <span class="lang-arrow">▼</span>
            <div id="languageDropdown" class="language-dropdown">
                ${AVAILABLE_LANGUAGES.map(lang => `
                    <div class="language-option ${lang.code === currentLanguage ? 'active' : ''}" 
                         onclick="selectLanguage('${lang.code}', event)">
                        <span class="lang-flag">${lang.flag}</span>
                        <span class="lang-name">${lang.name}</span>
                    </div>
                `).join('')}
            </div>
        </div>
    `;
}

// Toggle language dropdown
function toggleLanguageDropdown(event) {
    event.stopPropagation();
    const dropdown = document.getElementById('languageDropdown');
    if (dropdown) {
        dropdown.classList.toggle('show');
    }
}

// Select language from dropdown
function selectLanguage(langCode, event) {
    event.stopPropagation();
    setLanguage(langCode);
    
    const dropdown = document.getElementById('languageDropdown');
    if (dropdown) {
        dropdown.classList.remove('show');
    }
}

// Close dropdown when clicking outside
document.addEventListener('click', function(event) {
    const dropdown = document.getElementById('languageDropdown');
    if (dropdown && !event.target.closest('.language-selector')) {
        dropdown.classList.remove('show');
    }
});

// Initialize on load
initializeLanguage();
