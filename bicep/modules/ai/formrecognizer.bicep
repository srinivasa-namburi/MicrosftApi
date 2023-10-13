param uniqueName string
param location string
param tags object = {}


resource cognitiveService 'Microsoft.CognitiveServices/accounts@2023-05-01' = {
  name: 'formr-${uniqueName}'
  location: location
  tags: tags
  sku: {
    name: 'S0'
  }
  kind: 'FormRecognizer'
  properties: {
    disableLocalAuth: false // TODO replace with AAD auth
    apiProperties: {
      statisticsEnabled: false
    }
  }
}
