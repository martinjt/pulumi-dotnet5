# Example Pulumi for .NET 5 functions and Github Actions

This example repo shows how to setup a .NET 5 function to deploy to Azure using Pulumi via a Github action.

## Pre-requistes

1. Azure subscription (with the ability to create a service principal)
2. Azure Blob Storage account, with a container setup. 
3. Github repositry

## Environment Variables

`PULUMI_PASSPHRASE` this is required by pulumi, can be any string
`AZURE_PRINCIPAL` JSON representation of the an azure service principal that has contributor rights
`A

