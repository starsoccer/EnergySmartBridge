#!/bin/bash
set -e

echo Listing Versions!
nginx -v

while read key value; do
    export $key="${value}"
done < <(jq -r 'to_entries | map(.key + " " + (.value | tostring)) | .[]' /data/options.json)

declare -a BRIDGE_ARGS=(
    "-i"
    "-c" "/config/EnergySmartBridge.ini"
    "-e"
)
if [ "${DEBUG}" == "true" ]; then
    BRIDGE_ARGS+=("-d")
fi

exec nginx -g 'daemon off;' & mono EnergySmartBridge.exe "${BRIDGE_ARGS[@]}"
