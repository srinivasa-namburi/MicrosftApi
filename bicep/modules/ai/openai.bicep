param uniqueName string
param location string
param completionModel string
param completionModelVersion string
param embeddingModel string
param embeddingModelVersion string
param completionModelTPM int
param embeddingModelTPM int
param tags object = {}

resource openAI 'Microsoft.CognitiveServices/accounts@2023-05-01' = {
  name: 'openai-${uniqueName}'
  location: location
  kind: 'OpenAI'
  sku: {
    name: 'S0'
  }
  properties: {
    customSubDomainName: toLower(uniqueName)
  }
  tags: tags
}

resource openAI_completionModel 'Microsoft.CognitiveServices/accounts/deployments@2023-05-01' = {
  parent: openAI
  name: completionModel
  properties: {
    model: {
      format: 'OpenAI'
      name: completionModel
      version: completionModelVersion
    }
  }
  sku: {
    name: 'Standard'
    capacity: completionModelTPM
  }
}

resource openAI_embeddingModel 'Microsoft.CognitiveServices/accounts/deployments@2023-05-01' = {
  parent: openAI
  name: embeddingModel
  properties: {
    model: {
      format: 'OpenAI'
      name: embeddingModel
      version: embeddingModelVersion
    }
  }
    sku: {
      name: 'Standard'
      capacity: embeddingModelTPM
    }
  dependsOn: [// This "dependency" is to create models sequentially because the resource
    openAI_completionModel // provider does not support parallel creation of models properly.
  ]
}
