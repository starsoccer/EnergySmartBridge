FROM mono:latest AS build

WORKDIR /build
COPY ./src .
RUN nuget restore /build/EnergySmartBridge.sln
RUN msbuild /build/EnergySmartBridge.sln /t:Build /p:Configuration=Release
RUN mv /build/bin/Release /app

FROM nginx:1.24.0

RUN apt-get update && apt-get install -y openssl mono-devel jq

COPY nginx.conf /etc/nginx/nginx.conf

RUN openssl req  -nodes -new -x509 -sha1 -subj '/CN=energysmartwaterheater.com' -keyout /etc/nginx/energysmartwaterheater.com.key -out /etc/nginx/energysmartwaterheater.com.crt -days 3650

COPY --from=build /app .
COPY --from=build /app/EnergySmartBridge.ini /config/EnergySmartBridge.ini
COPY run.sh .

EXPOSE 443/tcp 8001/tcp

CMD [ "bash", "run.sh" ]
