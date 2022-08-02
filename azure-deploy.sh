#!/bin/bash

set -euo pipefail

# Change this if you are using your own github repository
gitSource="https://github.com/Azure-Samples/azure-sql-db-who-am-i.git"

# Azure configuration
FILE=".env"
if [[ -f $FILE ]]; then
	echo "loading from .env" 
    export $(egrep . $FILE | xargs -n1)
else
	cat << EOF > .env
ResourceGroup=""
AppName=""
Location=""
ConnectionStrings__AzureSQL="Server=.database.windows.net;Database=;UID=;PWD="
EOF
	echo "Enviroment file not detected."
	echo "Please configure values for your environment in the created .env file"
	echo "and run the script again."
	echo "ConnectionStrings__AzureSQL: connection string to connect to desired Azure SQL database"
	exit 1
fi

# Make sure connection string variable is set
if [[ -z "${ConnectionStrings__AzureSQL:-}" ]]; then
    echo "ConnectionStrings__AzureSQL not found."
	exit 1;
fi

echo "Creating Resource Group '$ResourceGroup'...";
az group create \
    -n $ResourceGroup \
    -l $Location

echo "Creating Application Service Plan '$AppName-plan'...";
az appservice plan create \
    -g $ResourceGroup \
    -n "$AppName-plan" \
    --is-linux \
    --sku B1     

echo "Creating Web Application '$AppName'...";
az webapp create \
    -g $ResourceGroup \
    -n $AppName \
    --plan "$AppName-plan" \
    --runtime "DOTNETCORE:6.0" \
    --deployment-source-url $gitSource \
    --deployment-source-branch main

echo "Configuring Connection String...";
az webapp config connection-string set \
    -g $ResourceGroup \
    -n $AppName \
    --settings AzureSQL=$ConnectionStrings__AzureSQL \
    --connection-string-type=SQLAzure

echo "Getting hostname..."
url=`az webapp show -g $ResourceGroup -n $AppName --query "defaultHostName" -o tsv`

echo "WebApp deployed at: https://$url"

echo "Done."