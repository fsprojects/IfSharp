FROM mono:5.4.0.201

RUN apt update \
    && apt install -y \
        python3-pip \
        git

RUN pip3 install --upgrade setuptools pip
RUN pip3 install jupyter

WORKDIR /
RUN git clone https://github.com/fsprojects/IfSharp.git

WORKDIR /IfSharp
RUN ./build.sh
RUN mono bin/ifsharp.exe --install

WORKDIR /
RUN mkdir notebooks
VOLUME notebooks

EXPOSE 8888

ENTRYPOINT ["jupyter", \
            "notebook", \
            "--no-browser", \
            "--ip='*'", \
            "--port=8888", \
            "--notebook-dir=/notebooks", \
	    "--allow-root" \
            ]
