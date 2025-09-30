# 🚨 DEPRECATED: This deployment guide has been replaced

> **⚠️ IMPORTANT: This document is no longer maintained**  
> The legacy Container Apps deployment approach documented here has been replaced by modern Kubernetes/Aspire 9.4-based deployment.

## ➡️ Use These Modern Deployment Guides Instead:

### For Azure DevOps Deployments
**📋 [DEPLOYMENT.ADO.md](./DEPLOYMENT.ADO.md)**  
Complete guide for deploying via Azure DevOps pipelines using the modern Kubernetes/Aspire architecture

### For GitHub Actions Deployments  
**🚀 [DEPLOYMENT.GitHub.md](./DEPLOYMENT.GitHub.md)**  
Complete guide for deploying via GitHub Actions using the modern Kubernetes/Aspire architecture

---

## Why the Change?

The modern deployment approach provides:
- **Single Source of Truth** - AppHost manages both dev experience AND production deployments
- **Kubernetes/AKS Native** - Better scaling, resilience, and cloud-native practices
- **Simplified CI/CD** - Two-stage pipelines (Aspire Publish → Deploy)
- **Better Private Networking** - Full private endpoint and hybrid deployment support
- **Application Gateway Integration** - Unified ingress with magic URLs
- **Enhanced SignalR** - Proper CDN compatibility and hosting flexibility

---

## 📚 Legacy Documentation (for reference only)

If you need to reference the old Container Apps deployment instructions for existing environments:

**[📖 Legacy Deployment Guide](./docs/deployment/DEPLOYMENT-LEGACY.md)**

> ⚠️ **Warning**: The legacy deployment approach should only be used for maintaining existing environments. All new deployments should use the modern guides above.

---

## 🔄 Migration Support

If you're migrating from the legacy Container Apps approach to the modern Kubernetes approach:

1. See the migration sections in both modern deployment guides
2. Review the **MIGRATION-GUIDE.md** for detailed step-by-step instructions
3. Test in a parallel environment before switching production workloads

---

## 🆘 Need Help?

- **Documentation Issues**: Check the modern deployment guides first
- **Migration Questions**: Refer to MIGRATION-GUIDE.md
- **Technical Support**: Create an issue with details about your deployment scenario

**Last Updated**: September 2025  
**Replacement Guides**: DEPLOYMENT.ADO.md, DEPLOYMENT.GitHub.md