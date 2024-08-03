import wandb
import hydra
from omegaconf import DictConfig, OmegaConf

from sac_driver import Driver

@hydra.main(version_base=None, config_path="config", config_name="config")
def main(cfg : DictConfig):
    print(cfg)
    driver = Driver(cfg) 
    driver.run()

if __name__ == "__main__":
    main()