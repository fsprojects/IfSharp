
# NOTE: If you modify this base image to use a different Linux distro
# then the install of the dotnet SDK may need to be changed
FROM mono:5.20.1.19

# Install Anaconda (conda / miniconda)
RUN apt update \
	&& apt install -y \
		apt-transport-https \
		wget \
		git
RUN wget https://repo.anaconda.com/miniconda/Miniconda3-latest-Linux-x86_64.sh -O ~/Miniconda3-latest-Linux-x86_64.sh
RUN chmod u+x ~/Miniconda3-latest-Linux-x86_64.sh
RUN ~/Miniconda3-latest-Linux-x86_64.sh -b -p /opt/miniconda
RUN . /opt/miniconda/bin/activate
ENV PATH="/opt/miniconda/bin:$PATH"

# Install dotnet SDK for debian
# see https://dotnet.microsoft.com/download/linux-package-manager/debian9/sdk-current
RUN apt install -y gpg
RUN wget -qO- https://packages.microsoft.com/keys/microsoft.asc | gpg --dearmor > microsoft.asc.gpg
RUN mv microsoft.asc.gpg /etc/apt/trusted.gpg.d/
RUN wget -q https://packages.microsoft.com/config/debian/9/prod.list
RUN mv prod.list /etc/apt/sources.list.d/microsoft-prod.list
RUN chown root:root /etc/apt/trusted.gpg.d/microsoft.asc.gpg
RUN chown root:root /etc/apt/sources.list.d/microsoft-prod.list
RUN apt-get update && apt-get install -y dotnet-sdk-2.2=2.2.105-1 && rm -rf /var/lib/opt/lists

# Test conda to ensure the above worked.
RUN conda -V

# Install Jupyter and extensions
RUN conda update -n base -c defaults conda
RUN conda install -c anaconda jupyter
RUN conda install -c conda-forge jupyter_contrib_nbextensions
RUN conda install -c conda-forge jupyter_nbextensions_configurator

# Install IfSharp
WORKDIR /
RUN git clone https://github.com/fsprojects/IfSharp.git
RUN mkdir notebooks
VOLUME notebooks

# Add user
RUN useradd -ms /bin/bash ifsharp-user
RUN chown -R ifsharp-user /notebooks && chown -R ifsharp-user /IfSharp
USER ifsharp-user

WORKDIR /IfSharp
RUN ./build.sh BuildNetFramework
RUN mono src/IfSharp/bin/Release/ifsharp.exe --install

# Install extensions and configurator
RUN jupyter contrib nbextension install --user
RUN jupyter nbextensions_configurator enable --user

EXPOSE 8888

# Final entrypoint
ENTRYPOINT ["jupyter", \
            "notebook", \
            "--no-browser", \
            "--ip='0.0.0.0'", \
            "--port=8888", \
            "--notebook-dir=/notebooks" \
            ]
